using System;
using System.Collections.Generic;
using System.Text;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// Generated CSV 원문을 헤더와 행 단위로 읽기 위한 경량 테이블 표현입니다.
    /// </summary>
    public sealed class BalanceCsvTable
    {
        #region Storage

        private readonly List<string> headers;
        private readonly List<BalanceCsvRow> rows;

        private BalanceCsvTable(string tableName, List<string> headers, List<BalanceCsvRow> rows)
        {
            TableName = tableName;
            this.headers = headers;
            this.rows = rows;
        }

        #endregion

        #region Metadata

        /// <summary>
        /// 오류 메시지에 표시할 테이블 이름입니다.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// 첫 번째 CSV 행에서 읽은 헤더 목록입니다.
        /// </summary>
        public IReadOnlyList<string> Headers => headers;

        /// <summary>
        /// 비어 있지 않은 데이터 행 목록입니다.
        /// </summary>
        public IReadOnlyList<BalanceCsvRow> Rows => rows;

        #endregion

        #region Query

        /// <summary>
        /// CSV 헤더가 존재하는지 확인합니다.
        /// </summary>
        public bool HasHeader(string header)
        {
            return headers.Contains(header);
        }

        #endregion

        #region Parsing

        /// <summary>
        /// CSV 원문을 테이블 객체로 변환합니다. 빈 행은 데이터 행에서 제외합니다.
        /// </summary>
        public static BalanceCsvTable FromText(string tableName, string csvText)
        {
            if (string.IsNullOrWhiteSpace(csvText))
            {
                return new BalanceCsvTable(tableName, new List<string>(), new List<BalanceCsvRow>());
            }

            List<List<string>> rawRows = ParseRows(csvText);
            if (rawRows.Count == 0)
            {
                return new BalanceCsvTable(tableName, new List<string>(), new List<BalanceCsvRow>());
            }

            List<string> headerRow = rawRows[0];
            List<string> parsedHeaders = new List<string>(headerRow.Count);
            for (int i = 0; i < headerRow.Count; i++)
            {
                parsedHeaders.Add((headerRow[i] ?? string.Empty).Trim());
            }

            List<BalanceCsvRow> parsedRows = new List<BalanceCsvRow>();
            for (int rowIndex = 1; rowIndex < rawRows.Count; rowIndex++)
            {
                List<string> rawRow = rawRows[rowIndex];
                bool empty = true;
                for (int i = 0; i < rawRow.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(rawRow[i]))
                    {
                        empty = false;
                        break;
                    }
                }

                if (empty)
                {
                    continue;
                }

                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int columnIndex = 0; columnIndex < parsedHeaders.Count; columnIndex++)
                {
                    string header = parsedHeaders[columnIndex];
                    if (string.IsNullOrEmpty(header))
                    {
                        continue;
                    }

                    string value = columnIndex < rawRow.Count ? rawRow[columnIndex] : string.Empty;
                    values[header] = value == null ? string.Empty : value.Trim();
                }

                parsedRows.Add(new BalanceCsvRow(tableName, rowIndex + 1, values));
            }

            return new BalanceCsvTable(tableName, parsedHeaders, parsedRows);
        }

        private static List<List<string>> ParseRows(string csvText)
        {
            List<List<string>> parsedRows = new List<List<string>>();
            List<string> currentRow = new List<string>();
            StringBuilder currentCell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char current = csvText[i];

                if (current == '"')
                {
                    if (inQuotes && i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        currentCell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && current == ',')
                {
                    currentRow.Add(currentCell.ToString());
                    currentCell.Length = 0;
                    continue;
                }

                if (!inQuotes && (current == '\r' || current == '\n'))
                {
                    if (current == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    {
                        i++;
                    }

                    currentRow.Add(currentCell.ToString());
                    currentCell.Length = 0;
                    parsedRows.Add(currentRow);
                    currentRow = new List<string>();
                    continue;
                }

                currentCell.Append(current);
            }

            currentRow.Add(currentCell.ToString());
            parsedRows.Add(currentRow);
            return parsedRows;
        }

        #endregion
    }

    /// <summary>
    /// CSV의 한 데이터 행과 해당 행의 테이블/줄 위치를 함께 보관합니다.
    /// </summary>
    public sealed class BalanceCsvRow
    {
        #region Storage

        private readonly Dictionary<string, string> values;

        internal BalanceCsvRow(string tableName, int rowNumber, Dictionary<string, string> values)
        {
            TableName = tableName;
            RowNumber = rowNumber;
            this.values = values;
        }

        #endregion

        #region Metadata

        /// <summary>
        /// 이 행이 속한 테이블 이름입니다.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// 원본 CSV 기준 1-based 행 번호입니다.
        /// </summary>
        public int RowNumber { get; }

        /// <summary>
        /// 검증 메시지에 사용할 table!row 형식 위치 문자열입니다.
        /// </summary>
        public string Location => TableName + "!row" + RowNumber;

        #endregion

        #region Accessors

        /// <summary>
        /// 헤더가 존재할 때 원문 문자열 값을 반환합니다.
        /// </summary>
        public bool TryGetString(string header, out string value)
        {
            if (values.TryGetValue(header, out value))
            {
                return true;
            }

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// 헤더가 없으면 빈 문자열을 반환합니다.
        /// </summary>
        public string GetString(string header)
        {
            return values.TryGetValue(header, out string value) ? value : string.Empty;
        }

        /// <summary>
        /// 지정 헤더 값을 int로 파싱합니다.
        /// </summary>
        public bool TryGetInt(string header, out int value)
        {
            return int.TryParse(GetString(header), out value);
        }

        /// <summary>
        /// 지정 헤더 값을 long으로 파싱합니다.
        /// </summary>
        public bool TryGetLong(string header, out long value)
        {
            return long.TryParse(GetString(header), out value);
        }

        /// <summary>
        /// TRUE/FALSE 또는 1/0 값을 bool로 파싱합니다.
        /// </summary>
        public bool TryGetBool(string header, out bool value)
        {
            string raw = GetString(header);
            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            if (raw == "1")
            {
                value = true;
                return true;
            }

            if (raw == "0")
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        #endregion
    }
}
