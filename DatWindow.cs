using ImGuiNET;
using System;
using System.Text;
using static ImGuiNET.ImGui;
using PoeFormats;
using System.IO;
using System.Collections.Generic;

namespace ImGui.NET.SampleProgram {
    internal class DatWindow {

        string startupDat = "characterstartqueststate";

        string datFolder = @"E:\Extracted\PathOfExile\3.25.Settlers\data";
        string failText = null;

        Schema schema;
        Dictionary<string, string> inputSchema;

        List<string> datFileList;
        int[] datRowCount;
        string[] datFileListSortedByRowCount;

        int datFileSelected;
        string titleCaseDatFileSelected;

        Dictionary<string, string> schemaText;
        string currentSchemaText;

        Dictionary<string, string> lowercaseToTitleCaseDats;

        Dat dat;

        //dat analaysis
        Dictionary<string, string[]> rowIds;
        int maxRows = 0;

        int selectedRow;
        int selectedColumn;

        int possibleRefMode = 0;

        string datName;
        string[] rows;
        Schema.Column[] columns;
        string[][] columnData;
        List<Schema.Column> tableColumns;
        string[] rowBytes;
        bool[] columnByteMode;
        

        bool byteView = false;

        //inspector results
        string inspectorBool;
        string inspectorInt;
        string inspectorFloat;
        string inspectorString;
        string inspectorRef;
        int inspectorRefValue;

        string inspectorIntArray;
        string inspectorFloatArray;
        string inspectorStringArray;
        string inspectorRefArray;
        string inspectorUnkArray;
        List<int> inspectorRefArrayValues;

        string possibleRefFilter = "";
        string possibleRefValueFilter = "";

        DatAnalysis[] columnAnalysis;

        string ToHexSpaced(ReadOnlySpan<byte> b, int start = 0, int length = int.MaxValue) {
            if (start + length > b.Length) length = b.Length - start;
            if (b.Length <= start || length == 0) return "";

            StringBuilder s = new StringBuilder(b.Length * 3 - 1);
            s.Append(b[0].ToString("X2"));
            for(int i = 1; i < b.Length; i++) {
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
            for(int i = 0; i < t.Length; i++) {
                s.Append(t[i].ToString());
                s.Append(", ");
            }
            if (t.Length > 0) s.Remove(s.Length - 2, 2);
            s.Append("]");
            return s.ToString();
        }

        public DatWindow(string path) {
            datFileList = new List<string>();
            foreach(string datPath in Directory.EnumerateFiles(datFolder, "*.dat64")) {
                datFileList.Add(Path.GetFileNameWithoutExtension(datPath));
            }

            schemaText = Schema.SplitGqlTypes(@"E:\Projects2\dat-schema\dat-schema");
            lowercaseToTitleCaseDats = new Dictionary<string, string>();
            foreach(string dat in schemaText.Keys) lowercaseToTitleCaseDats[dat.ToLower()] = dat;

            schema = new Schema(@"E:\Projects2\dat-schema\dat-schema");

            datRowCount = new int[datFileList.Count];
            datFileListSortedByRowCount = new string[datFileList.Count];
            rowIds = new Dictionary<string, string[]>();
            for (int i = 0; i < datFileList.Count; i++) {
                
                string datFile = datFileList[i];
                datFileListSortedByRowCount[i] = datFile;
                string datPath = Path.Combine(datFolder, datFile + ".dat64");
                Dat d = new Dat(datPath);
                datRowCount[i] = d.rowCount;
                if (d.rowCount > maxRows) maxRows = d.rowCount;

                if (lowercaseToTitleCaseDats.ContainsKey(datFile)) {
                    string titleCaseDat = lowercaseToTitleCaseDats[datFile];
                    if(schema.schema.ContainsKey(titleCaseDat)) {
                        var columns = schema.schema[titleCaseDat];
                        if (columns.Length != 0 && columns[0].type == Schema.Column.Type.@string) {
                            rowIds[titleCaseDat] = d.Column(columns[0]);
                        } else if (columns.Length > 1 && columns[1].type == Schema.Column.Type.@string) {
                            rowIds[titleCaseDat] = d.Column(columns[1]);
                        }
                    }
                }

            }

            Array.Sort<int, string>(datRowCount, datFileListSortedByRowCount);

            LoadDat(startupDat);

            //rows = new string[Math.Min(dat.rowCount, 10)];
            //for (int i = 0; i < rows.Length; i++) {
            //    rows[i] = ToHexSpaced(dat.rows[i]);
            //}

        }

        void UpdateSchema(string text) {
            Schema.GqlReader r = new Schema.GqlReader(" " + text + " "); //TODO tokeniser doesn't work if start or end are proper characters lol
            schema.ParseGql(r);
            string fileTitleCase = lowercaseToTitleCaseDats[datFileList[datFileSelected]];
            schemaText[fileTitleCase] = text;
            LoadDat(datFileList[datFileSelected]); //TODO this is pretty wasteful
        }

        void LoadDat(string filename) {
            
            if (datFileList[datFileSelected] != filename) {
                for (int i = 0; i < datFileList.Count; i++) {
                    if (datFileList[i] == filename) {
                        datFileSelected = i;
                        break;
                    }
                }
            }
            
            if (!lowercaseToTitleCaseDats.ContainsKey(filename)) {
                currentSchemaText = null;
                failText = $"{filename} has no schema"; return;
            }
            string fileTitleCase = lowercaseToTitleCaseDats[filename];
            titleCaseDatFileSelected = fileTitleCase;


            currentSchemaText = schemaText[fileTitleCase];
            if (!currentSchemaText.StartsWith("type")) {
                currentSchemaText = null;
                failText = $"{filename} is not type"; return;
            }

            string datPath = Path.Combine(datFolder, filename + ".dat64");
            if (!File.Exists(datPath)) {
                failText = $"{datPath} does not exist"; return;
            }


            failText = null;
            dat = new Dat(datPath);
            datName = filename;

            columns = schema.schema[fileTitleCase];
            columnData = new string[columns.Length][];

            for (int i = 0; i < columns.Length; i++) {
                columnData[i] = dat.Column(columns[i], rowIds);
            }

            tableColumns = new List<Schema.Column>();

            int byteIndex = 0;
            int columnIndex = 0;
            while(byteIndex < dat.rowWidth) {
                if(columnIndex < columns.Length && columns[columnIndex].offset == byteIndex) {
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
                //rowBytes[i] = Convert.ToHexString(dat.Row(i));
                rowBytes[i] = ToHexSpaced(dat.Row(i));
            }

            columnAnalysis = new DatAnalysis[tableColumns.Count];
            for(int i = 0; i < columnAnalysis.Length; i++) {
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

        bool AnalyseFloat(float f) {
            if (f < 0.00001 && f > -0.00001 && f != 0) return false;
            if (f > 1000000000 || f < -1000000000) return false;
            return true;
        }

        void Analyse(int row, int columnOffset) {
            byte[] data = dat.data;
            byte[] varying = dat.varying;
            int offset = dat.rowWidth * row + columnOffset;
            int distToEnd = dat.rowWidth - columnOffset;

            if(distToEnd <= 0) {
                inspectorBool = "OOB";
            } else {
                byte b = data[offset];
                if (b > 1) {
                    inspectorBool = $"!!! (greater than one {(int)b})";
                }
                else {
                    inspectorBool = b == 1 ? "True" : "False";
                }

            }


            if(distToEnd < 4) {
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
                    }else if (longLower >= maxRows) {
                        inspectorRef = $"row index too large {longLower})";
                    } else {
                        inspectorRefValue = (int)longLower;
                        inspectorRef = longLower.ToString();
                    }

                    //arrays
                    if(longLower < 0) {
                        SetInspectorArrayValues($"negative count {longLower}");
                    }  else if(longUpper < 0) {
                        SetInspectorArrayValues($"negative offset {longUpper}");
                    } else if (longLower > DatAnalysis.arrayMaxCount) {
                        SetInspectorArrayValues($"array size implausibly big {longLower}");
                    } else {
                        long lengthToEnd = varying.Length - longUpper;


                        //unk array
                        if (lengthToEnd <= 0) {
                            inspectorUnkArray = $"!!! (offset too big {longUpper})";
                        }
                        else {
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

        public unsafe void Update() {

            if (BeginTable("MAIN", 3)) {
                TableSetupColumn("Dat Files", ImGuiTableColumnFlags.WidthFixed, 256);
                TableSetupColumn("Data", ImGuiTableColumnFlags.WidthStretch);
                TableSetupColumn("Schema", ImGuiTableColumnFlags.WidthFixed, 512);
                TableHeadersRow();
                TableNextRow();


                //DAT FILES
                TableSetColumnIndex(0);
                bool a = true;
                Checkbox("BYTE VIEW", ref byteView);
                if (BeginListBox("##FILELIST", new System.Numerics.Vector2(256, 1024))) {
                    for (int i = 0; i < datFileList.Count; i++) { 
                        bool isSelected = datFileSelected == i;

                        if (Selectable(datFileList[i], isSelected)) {
                            if(datFileSelected != i) {
                                datFileSelected = i;
                                LoadDat(datFileList[i]);
                            }

                        }

                           
                    }
                    EndListBox();
                }

                //DATA
                TableSetColumnIndex(1);
                if(failText == null) {
                    if(byteView) {
                        if (BeginTable("BYTETABLE", dat.rowWidth + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY)) {
                            //TableSetupScrollFreeze(1, 1);

                            TableSetupColumn("IDX");
                            for (int i = 0; i < dat.rowWidth; i++) {
                                TableSetupColumn(i.ToString());
                            }
                            TableHeadersRow();

                            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                            clipper.Begin(dat.rowCount);
                            while (clipper.Step()) {
                                for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++) {
                                    TableNextRow();
                                    TableSetColumnIndex(0);
                                    Text(row.ToString()); //TODO garbagio
                                    for (int col = 0; col < dat.rowWidth; col++) {
                                        TableSetColumnIndex(col + 1);
                                        Text(rowBytes[row].AsSpan().Slice(col * 3, 2));
                                    }
                                }
                            }
                            EndTable();
                        }
                    }
                    else {
                        if (BeginTable(datFileList[datFileSelected], tableColumns.Count + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable)) {
                            TableSetupScrollFreeze(tableColumns[0].name == "Id" ? 2 : 1, 1);

                            TableSetupColumn("IDX");
                            for (int i = 0; i < tableColumns.Count; i++) {
                                var column = tableColumns[i];
                                //TODO garbage
                                if(column.type == Schema.Column.Type.Byte) {
                                    TableSetupColumn(column.offset.ToString());
                                } else {
                                    TableSetupColumn(column.array ? $"{column.name}\n[{column.type}]\n{column.offset}" : $"{column.name}\n{column.type}\n{column.offset}");
                                }
                            }
                            TableNextRow();
                            TableSetColumnIndex(0);
                            TableHeader("Row");

                            for (int i = 0; i < tableColumns.Count; i++) {
                                TableSetColumnIndex(i + 1);
                                string columnName = TableGetColumnName(i + 1);
                                PushID(i);
                                var column = tableColumns[i];

                                if (column.type != Schema.Column.Type.Byte)
                                    Checkbox("##byte", ref columnByteMode[i]);
                                //SameLine();
                                DatAnalysis an = columnAnalysis[i];
                                bool error = an.GetError(column) != DatAnalysis.Error.NONE;

                                if (error) PushStyleColor(ImGuiCol.TableHeaderBg, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                                TableHeader(columnName);
                                if(error) PopStyleColor();
                                PopID();
                            }
                            //TableHeadersRow();
                            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                            clipper.Begin(dat.rowCount);
                            while (clipper.Step()) {
                                for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++) {
                                    TableNextRow();
                                    TableSetColumnIndex(0);
                                    if(selectedRow == row) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(0.3f, 0.5f, 1, 0.3f)));
                                    Text(row.ToString()); //TODO garbagio
                                    for (int col = 0; col < tableColumns.Count; col++) {
                                        TableSetColumnIndex(col + 1);

                                        var column = tableColumns[col];

                                        if (column.type == Schema.Column.Type.rid)
                                            TableSetBgColor(ImGuiTableBgTarget.CellBg, GetColorU32(new System.Numerics.Vector4(0, 1, 0, 0.1f)));
                                        if (column.type == Schema.Column.Type.Byte || columnByteMode[col]) {
                                            ReadOnlySpan<char> text = rowBytes[row].AsSpan().Slice(column.offset * 3, column.Size() * 3 - 1);
                                            bool isZero = true;
                                            for(int i = 0; i < column.Size(); i++)
                                                if(dat.Row(row)[column.offset + i] != 0)
                                                    isZero = false;
                                            PushID(row * tableColumns.Count + col);
                                            if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                            if (Selectable(text, selectedColumn == col && selectedRow == row)) {

                                                selectedColumn = col;
                                                selectedRow = row;
                                                Analyse(row, column.offset);
                                            }
                                            if (isZero) PopStyleColor();
                                            PopID();
                                        } else {
                                            string text = columnData[col][row];
                                            bool isZero = text == "0" || text == "False";

                                            PushID(row * tableColumns.Count + col);
                                            if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                            if (Selectable(text, selectedColumn == col && selectedRow == row)) {
                                                
                                                selectedColumn = col;
                                                selectedRow = row;
                                                Analyse(row, column.offset);
                                            }
                                            if (isZero) PopStyleColor();
                                            PopID();
                                        }
                                    }
                                }
                            }
                            EndTable();
                        }
                    }



                } else {
                    Text(failText);
                }

                //SCHEMA
                {
                    if(currentSchemaText != null) {
                        TableSetColumnIndex(2);
                        InputTextMultiline("", ref currentSchemaText, 65535, new System.Numerics.Vector2(512, 768));

                        if (Button("Parse")) {
                            UpdateSchema(currentSchemaText);
                        }
                        SameLine();
                        if (Button("Reset")) {
                            string fileTitleCase = lowercaseToTitleCaseDats[datFileList[datFileSelected]];
                            currentSchemaText = schemaText[fileTitleCase];
                        }
                    }
                    if(selectedColumn != -1) {
                        //INSPECTOR
                        DatAnalysis tempAnalysis = columnAnalysis[selectedColumn];

                        Text("Inspector");
                        if (BeginTable("Inspector", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {

                            InspectorRow("Ref Array", inspectorRefArray, tempAnalysis.isRefArray.ToString(), tempAnalysis.isRefArray == DatAnalysis.Error.NONE);
                            InspectorRow("String Array", inspectorStringArray, tempAnalysis.isStringArray.ToString(), tempAnalysis.isStringArray == DatAnalysis.Error.NONE);
                            InspectorRow("Float Array", inspectorFloatArray, tempAnalysis.isFloatArray.ToString(), tempAnalysis.isFloatArray == DatAnalysis.Error.NONE);
                            InspectorRow("Int Array", inspectorIntArray, tempAnalysis.isIntArray.ToString(), tempAnalysis.isIntArray == DatAnalysis.Error.NONE);
                            InspectorRow("Unknown Array", inspectorUnkArray, tempAnalysis.isArray.ToString(), tempAnalysis.isArray == DatAnalysis.Error.NONE);
                            InspectorRow("Reference", inspectorRef, tempAnalysis.isRef.ToString(), tempAnalysis.isRef == DatAnalysis.Error.NONE);
                            InspectorRow("String", inspectorString, tempAnalysis.isString.ToString(), tempAnalysis.isString == DatAnalysis.Error.NONE);
                            InspectorRow("Float", inspectorFloat, tempAnalysis.isFloat.ToString(), tempAnalysis.isFloat == DatAnalysis.Error.NONE);
                            InspectorRow("Bool", inspectorBool, tempAnalysis.isBool.ToString(), tempAnalysis.isBool == DatAnalysis.Error.NONE);
                            InspectorRow("Int", inspectorInt, tempAnalysis.isInt.ToString(), tempAnalysis.isInt == DatAnalysis.Error.NONE);
                            EndTable();
                        }

                        bool showRefValues = tempAnalysis.isRef == DatAnalysis.Error.NONE && inspectorRefValue >= 0;
                        bool showRefArrayValues = tempAnalysis.isRefArray == DatAnalysis.Error.NONE && inspectorRefArrayValues != null && inspectorRefArrayValues.Count > 0;
                        if (showRefValues || showRefArrayValues) {

                            bool core = schema.tableFiles[titleCaseDatFileSelected] == "_Core";

                            Text("Possible Refs");
                            SameLine();
                            RadioButton("All", ref possibleRefMode, 0); SameLine();
                            if (core) BeginDisabled();
                            RadioButton("Sibling", ref possibleRefMode, 1);
                            if (core) EndDisabled();
                            SameLine();
                            RadioButton("Core", ref possibleRefMode, 2); SameLine();
                            if (core) BeginDisabled();
                            RadioButton("Core+Sib", ref possibleRefMode, 3);
                            if (core) EndDisabled();
                            SameLine();
                            RadioButton("No Schema", ref possibleRefMode, 4);
                            PushItemWidth(158);
                            InputText("Filter Table", ref possibleRefFilter, 256);
                            SameLine();
                            PushItemWidth(158);
                            InputText("Filter Values", ref possibleRefValueFilter, 256);

                        }

                        if (showRefArrayValues) {

                            //TODO shouldn't be building these strings every frame lol
                            if (BeginTable("Array Possible Refs", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame)) {
                                for (int i = 0; i < datFileListSortedByRowCount.Length; i++) {
                                    if (datRowCount[i] > tempAnalysis.maxRefArray) {
                                        string table = datFileListSortedByRowCount[i];
                                        if (possibleRefFilter.Length > 0 && !table.Contains(possibleRefFilter)) continue;
                                        if (lowercaseToTitleCaseDats.ContainsKey(table)) {
                                            table = lowercaseToTitleCaseDats[table];
                                            if (possibleRefMode == 4) continue;
                                        } else if (possibleRefMode != 4 && possibleRefMode != 0) continue;
                                        if (schema.enums.ContainsKey(table))
                                            continue;
                                        if (schema.tableFiles.ContainsKey(table)) {
                                            string file = schema.tableFiles[table];
                                            bool core = file == "_Core";
                                            bool sibling = file == schema.tableFiles[titleCaseDatFileSelected];
                                            if (possibleRefMode == 1 && !sibling) continue;
                                            if (possibleRefMode == 2 && !core) continue;
                                            if (possibleRefMode == 3 && !sibling && !core) continue;
                                        }

                                        StringBuilder s = new StringBuilder("[");
                                        for (int row = 0; row < inspectorRefArrayValues.Count; row++) {
                                            if (rowIds.ContainsKey(table))
                                                s.Append(rowIds[table][inspectorRefArrayValues[row]]);
                                            else
                                                s.Append(inspectorRefArrayValues[row].ToString());
                                            s.Append(", ");
                                        }
                                        if (s.Length > 1) s.Remove(s.Length - 2, 2); s.Append(']');
                                        string ss = s.ToString();

                                        if (possibleRefValueFilter.Length > 0 && !ss.ToLower().Contains(possibleRefValueFilter)) continue;

                                        TableNextRow();
                                        TableSetColumnIndex(0); Text($"{table} ({datRowCount[i]})");
                                        TableSetColumnIndex(1);


                                        Text(ss);
                                        SetItemTooltip(ss);
                                    }
                                    //string datFile
                                }
                                EndTable();
                            }
                        }

                        if (showRefValues) {
                            if (BeginTable("Possible Refs", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame)) {
                                for (int i = 0; i < datFileListSortedByRowCount.Length; i++) {
                                    if (datRowCount[i] > tempAnalysis.maxRef) {
                                        string table = datFileListSortedByRowCount[i];
                                        if (possibleRefFilter.Length > 0 && !table.Contains(possibleRefFilter)) continue;
                                        if (lowercaseToTitleCaseDats.ContainsKey(table)) {
                                            table = lowercaseToTitleCaseDats[table];
                                            if (possibleRefMode == 4) continue;
                                        } else if (possibleRefMode != 4 && possibleRefMode != 0) continue;
                                        if (schema.enums.ContainsKey(table))
                                            continue;
                                        if (schema.tableFiles.ContainsKey(table)) {
                                            string file = schema.tableFiles[table];
                                            bool core = file == "_Core";
                                            bool sibling = file == schema.tableFiles[titleCaseDatFileSelected];
                                            if (possibleRefMode == 1 && !sibling) continue;
                                            if (possibleRefMode == 2 && !core) continue;
                                            if (possibleRefMode == 3 && !sibling && !core) continue;
                                        }
                                        string s = rowIds.ContainsKey(table) ? rowIds[table][inspectorRefValue] : inspectorRefValue.ToString();

                                        if (possibleRefValueFilter.Length > 0 && !s.ToLower().Contains(possibleRefValueFilter)) continue;
                                        TableNextRow();
                                        TableSetColumnIndex(0); Text($"{table} ({datRowCount[i]})");
                                        TableSetColumnIndex(1); Text(s); SetItemTooltip(s);


                                    }
                                    //string datFile
                                }
                                EndTable();
                            }
                        }
                    }


                    //Text(selectedColumn.ToString());
                    //Text(selectedRow.ToString());
                }
                
                EndTable();
            }

        }

        void SetInspectorArrayValues(string s) {
            inspectorIntArray = s;
            inspectorFloatArray = s;
            inspectorStringArray = s;
            inspectorRefArray = s;
            inspectorUnkArray = s;
        }

        void InspectorRow(string label, string value, string tooltip, bool analysis) {
            if (value == null) return;
            TableNextRow();
            if (!analysis) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
            TableSetColumnIndex(0); Text(label); SetItemTooltip(tooltip);
            TableSetColumnIndex(1); Text(value); SetItemTooltip(tooltip);
        }
    }
}
