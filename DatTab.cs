using PoeFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImGui.NET.SampleProgram {
    public class DatTab {
        public string schemaText;
        public Dat dat;
        public string name;
        public string nameLower;

        public int selectedRow;
        public int selectedColumn;

        Schema.Column[] columns;
        public string[][] columnData;
        public List<Schema.Column> tableColumns;
        public string[] rowBytes;
        public bool[] columnByteMode;

        public DatAnalysis[] columnAnalysis;

        //inspector results
        public string inspectorBool;
        public string inspectorInt;
        public string inspectorFloat;
        public string inspectorString;
        public string inspectorRef;
        public int inspectorRefValue;

        public string inspectorIntArray;
        public string inspectorFloatArray;
        public string inspectorStringArray;
        public string inspectorRefArray;
        public string inspectorUnkArray;
        public List<int> inspectorRefArrayValues;

        int maxRows;

        public override string ToString() {
            return name;
        }

        public DatTab(string tableName, string schemaText, string datPath, Schema schema, Dictionary<string, string[]> rowIds, int maxRows) {
            name = tableName;
            nameLower = tableName.ToLower();
            this.schemaText = schemaText;
            dat = new Dat(datPath);
            this.maxRows = maxRows;

            columns = schema.schema[name];
            columnData = new string[columns.Length][];

            for (int i = 0; i < columns.Length; i++) {
                columnData[i] = dat.Column(columns[i], rowIds);
            }

            tableColumns = new List<Schema.Column>();

            int byteIndex = 0;
            int columnIndex = 0;
            while (byteIndex < dat.rowWidth) {
                if (columnIndex < columns.Length && columns[columnIndex].offset == byteIndex) {
                    var column = columns[columnIndex];
                    tableColumns.Add(column);
                    columnIndex++;
                    byteIndex += column.Size();

                } else {
                    tableColumns.Add(new Schema.Column(byteIndex));
                    byteIndex++;
                }
            }
            columnByteMode = new bool[tableColumns.Count];

            rowBytes = new string[dat.rowCount];
            for (int i = 0; i < rowBytes.Length; i++) {
                rowBytes[i] = ToHexSpaced(dat.Row(i));
            }

            columnAnalysis = new DatAnalysis[tableColumns.Count];
            for (int i = 0; i < columnAnalysis.Length; i++) {
                columnAnalysis[i] = new DatAnalysis(dat, tableColumns[i].offset, maxRows);
            }

            selectedRow = -1;
            selectedColumn = -1;
            inspectorBool = null;
            inspectorInt = null;
            inspectorFloat = null;
            inspectorRef = null;
            inspectorString = null;
            inspectorIntArray = null;
            inspectorFloatArray = null;
            inspectorStringArray = null;
            inspectorRefArray = null;
            inspectorUnkArray = null;

        }

        string ToHexSpaced(ReadOnlySpan<byte> b, int start = 0, int length = int.MaxValue) {
            if (start + length > b.Length) length = b.Length - start;
            if (b.Length <= start || length == 0) return "";

            StringBuilder s = new StringBuilder(b.Length * 3 - 1);
            s.Append(b[0].ToString("X2"));
            for (int i = 1; i < b.Length; i++) {
                s.Append(' ');
                s.Append(b[i].ToString("X2"));
            }
            return s.ToString();
        }

        string MakeArrayString<T>(T val, int count) {
            string sVal = val.ToString();
            StringBuilder s = new StringBuilder("[");
            for (int i = 0; i < count; i++) {
                s.Append(sVal);
                s.Append(", ");
            }
            if (count > 0) s.Remove(s.Length - 2, 2);
            s.Append("]");
            return s.ToString();
        }

        string MakeArrayString<T>(T[] t) {
            StringBuilder s = new StringBuilder("[");
            for (int i = 0; i < t.Length; i++) {
                s.Append(t[i].ToString());
                s.Append(", ");
            }
            if (t.Length > 0) s.Remove(s.Length - 2, 2);
            s.Append("]");
            return s.ToString();
        }


        void SetInspectorArrayValues(string s) {
            inspectorIntArray = s;
            inspectorFloatArray = s;
            inspectorStringArray = s;
            inspectorRefArray = s;
            inspectorUnkArray = s;
        }

        public void Analyse(int row, int columnOffset) {
            byte[] data = dat.data;
            byte[] varying = dat.varying;
            int offset = dat.rowWidth * row + columnOffset;
            int distToEnd = dat.rowWidth - columnOffset;

            if (distToEnd <= 0) {
                inspectorBool = "OOB";
            } else {
                byte b = data[offset];
                if (b > 1) {
                    inspectorBool = $"!!! (greater than one {(int)b})";
                } else {
                    inspectorBool = b == 1 ? "True" : "False";
                }

            }


            if (distToEnd < 4) {
                inspectorInt = "OOB";
                inspectorFloat = "OOB";
            } else {
                int intValue = BitConverter.ToInt32(data, offset);
                inspectorInt = intValue.ToString();

                float floatValue = BitConverter.ToSingle(data, offset);
                inspectorFloat = floatValue.ToString();
            }


            //8 byte types (string)
            if (distToEnd < 8) {
                inspectorString = "OOB";
                inspectorRef = "OOB";
                SetInspectorArrayValues("OOB");
            } else {
                long longLower = BitConverter.ToInt64(data, offset);
                if (longLower < 8 || longLower + 1 >= varying.Length) {
                    inspectorString = $"OFFSET OOB {longLower}";
                } else if (longLower % 2 == 1) {
                    inspectorString = $"OFFSET NOT EVEN {longLower}";
                } else {
                    inspectorString = Dat.ReadWStringNullTerminated(varying, (int)longLower);
                }

                //16 byte types
                if (distToEnd < 16) {
                    inspectorRef = "OOB";
                    SetInspectorArrayValues("OOB");
                } else {
                    //ref
                    inspectorRefValue = -1;
                    long longUpper = BitConverter.ToInt64(data, offset + 8); //if less than 16 bytes from end of data
                    if (longLower == -72340172838076674 && longUpper == -72340172838076674) {
                        inspectorRef = "null";
                    } else if (longUpper != 0) {
                        inspectorRef = $"bytes 8-16 non zero {longUpper}";
                    } else if (longLower < 0) {
                        inspectorRef = $"negative row index {longLower}";
                    } else if (longLower >= maxRows) {
                        inspectorRef = $"row index too large {longLower})";
                    } else {
                        inspectorRefValue = (int)longLower;
                        inspectorRef = longLower.ToString();
                    }

                    //arrays
                    if (longLower < 0) {
                        SetInspectorArrayValues($"negative count {longLower}");
                    } else if (longUpper < 0) {
                        SetInspectorArrayValues($"negative offset {longUpper}");
                    } else if (longLower > DatAnalysis.arrayMaxCount) {
                        SetInspectorArrayValues($"array size implausibly big {longLower}");
                    } else {
                        long lengthToEnd = varying.Length - longUpper;


                        //unk array
                        if (lengthToEnd <= 0) {
                            inspectorUnkArray = $"!!! (offset too big {longUpper})";
                        } else {
                            StringBuilder s = new StringBuilder("[");
                            inspectorUnkArray = MakeArrayString("?", (int)longLower);
                        }

                        //4 byte array
                        if (lengthToEnd < longLower * 4) {
                            inspectorIntArray = $"no room for {longLower} ints";
                            inspectorFloatArray = $"no room for {longLower} floats";
                        } else {

                            int[] ints = new int[longLower];
                            float[] floats = new float[longLower];

                            for (int i = 0; i < longLower; i++) {
                                ints[i] = BitConverter.ToInt32(varying, (int)longUpper + 4 * i);
                                float f = BitConverter.ToSingle(varying, (int)longUpper + 4 * i);
                                floats[i] = f;
                            }
                            inspectorIntArray = MakeArrayString(ints);
                            inspectorFloatArray = MakeArrayString(floats);
                        }

                        //8 byte array (string)
                        if (lengthToEnd < longLower * 8) {
                            inspectorStringArray = $"no room for {longLower} string offsets";
                        } else {
                            string[] strings = new string[longLower];
                            for (int i = 0; i < longLower; i++) {
                                long strOffset = BitConverter.ToInt64(varying, (int)longUpper + 8 * i);
                                if (strOffset < 0 || strOffset + 1 >= varying.Length) {
                                    strings[i] = $"OOB {strOffset}";
                                } else {
                                    strings[i] = Dat.ReadWStringNullTerminated(varying, (int)strOffset);
                                }
                            }
                            inspectorStringArray = MakeArrayString(strings);
                        }

                        //16 byte array (refs)

                        if (lengthToEnd < longLower * 16) {
                            inspectorRefArray = $"no room for {longLower} refs";
                        } else {
                            inspectorRefArrayValues = new List<int>();

                            string[] refs = new string[longLower];
                            for (int i = 0; i < longLower; i++) {
                                long refLower = BitConverter.ToInt64(varying, (int)longUpper + 16 * i);
                                long refUpper = BitConverter.ToInt64(varying, (int)longUpper + 16 * i + 8);
                                if (refLower == -72340172838076674 && refUpper == -72340172838076674) {
                                    refs[i] = "null"; //TODO why would there be null values in array?
                                } else if (refUpper != 0) {
                                    refs[i] = $"bytes 8-16 non zero {refUpper}";
                                } else if (refLower < 0) {
                                    refs[i] = $"negative row index {refLower}";
                                } else if (refLower > maxRows) {
                                    refs[i] = $"row index too large {refLower})";
                                } else {
                                    refs[i] = refLower.ToString();
                                    inspectorRefArrayValues.Add((int)refLower); //TODO what about invalid values?
                                }
                            }
                            inspectorRefArray = MakeArrayString(refs);
                        }
                    }
                }
            }
        }


    }
}
