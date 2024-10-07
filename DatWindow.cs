using ImGuiNET;
using System;
using System.Text;
using static ImGuiNET.ImGui;
using PoeFormats;
using System.IO;
using System.Collections.Generic;

namespace ImGui.NET.SampleProgram {
    internal class DatWindow {

        string datFolder = @"E:\Extracted\PathOfExile\3.25.Settlers\data";
        string failText = null;

        Schema schema;
        Dictionary<string, string> inputSchema;

        List<string> datFileList;
        int datFileSelected;

        Dictionary<string, string> schemaText;
        string currentSchemaText;

        Dictionary<string, string> lowercaseToTitleCaseDats;

        Dat dat;

        //dat analaysis
        Dictionary<string, string[]> rowIds;
        int maxRows = 0;

        int selectedRow;
        int selectedColumn;


        string datName;
        string[] rows;
        Schema.Column[] columns;
        string[][] columnData;
        List<Schema.Column> tableColumns;
        string[] rowBytes;
        bool[] columnByteMode;

        bool byteView = false;

        //inspector results
        string inspectorBool; bool analysisBool;
        string inspectorInt; bool analysisInt;
        string inspectorFloat; bool analysisFloat;
        string inspectorString; bool analysisString;
        string inspectorRef; bool analysisRef;

        string inspectorIntArray; bool analysisIntArray;
        string inspectorFloatArray; bool analysisFloatArray;
        string inspectorStringArray; bool analysisStringArray;
        string inspectorRefArray; bool analysisRefArray;

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

        public DatWindow(string path) {
            datFileList = new List<string>();
            foreach(string datPath in Directory.EnumerateFiles(datFolder, "*.dat64")) {
                datFileList.Add(Path.GetFileNameWithoutExtension(datPath));
            }

            schemaText = Schema.SplitGqlTypes(@"E:\Projects2\dat-schema\dat-schema");
            lowercaseToTitleCaseDats = new Dictionary<string, string>();
            foreach(string dat in schemaText.Keys) lowercaseToTitleCaseDats[dat.ToLower()] = dat;

            schema = new Schema(@"E:\Projects2\dat-schema\dat-schema");

            rowIds = new Dictionary<string, string[]>();
            foreach(string table in schema.schema.Keys) {
                var columns = schema.schema[table];
                if (columns[0].type == Schema.Column.Type.@string && columns[0].name == "Id") {
                    string datPath = Path.Combine(datFolder, table.ToLower() + ".dat64");
                    if(File.Exists(datPath)) {
                        Dat d = new Dat(datPath);
                        if(d.rowCount > maxRows) maxRows = d.rowCount;
                        rowIds[table] = d.Column(columns[0]);
                    }
                }
            }


            LoadDat("chests");

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
        }

        bool AnalyseFloat(float f) {
            if (f < 0.00001 && f > -0.00001 && f != 0) return false;
            if (f < 1000000000 || f > -1000000000) return false;
            return true;
        }

        void Analyse(int row, int columnOffset) {
            byte[] data = dat.data;
            byte[] varying = dat.varying;
            int offset = dat.rowWidth * row + columnOffset;

            byte b = data[offset];
            if(b > 1) {
                inspectorBool = $"!!! (greater than one {(int)b})";
                analysisBool = false;
            } else {
                inspectorBool = b == 1 ? "True" : "False";
                analysisBool = true;
            }
            

            int intValue = BitConverter.ToInt32(data, offset);
            inspectorInt = intValue.ToString();
            analysisInt = true;

            float floatValue = BitConverter.ToSingle(data, offset);
            analysisFloat = AnalyseFloat(floatValue);
            inspectorFloat = floatValue.ToString();

            analysisRef = false;
            long longLower = BitConverter.ToInt64(data, offset);
            long longUpper = BitConverter.ToInt64(data, offset + 8);

            //ref
            if(longLower == -72340172838076674 && longUpper == -72340172838076674) {
                inspectorRef = "NULL VALUE";
                analysisRef = true;
            } else if (longUpper != 0) {
                inspectorRef = $"!!! (bytes 8-16 non zero {longUpper})";
            } else {
                if (longLower < 0)
                    inspectorRef = $"!!! (negative value {longLower})";
                else if (longLower >= maxRows)
                    inspectorRef = $"!!! (too large value {longLower})";
                else {
                    analysisRef = true;
                    inspectorRef = longLower.ToString();
                }
            }

            //string
            analysisString = false;
            analysisIntArray = false;
            analysisFloatArray = false;
            analysisStringArray = false;
            analysisRefArray = false;
            if (longLower < 0) {
                inspectorString = $"!!! (negative offset {longLower})";

                inspectorIntArray = $"!!! (negative count {longLower})";
                inspectorFloatArray = inspectorIntArray;
                inspectorStringArray = inspectorIntArray;
                inspectorRefArray = inspectorIntArray;

            }
            else {
                if (longLower + 1 >= varying.Length) {
                    inspectorString = $"!!! (offset too big {longLower})";
                }
                else {
                    analysisString = true;
                    inspectorString = Dat.ReadWStringNullTerminated(varying, (int)longLower);
                }

                if (longUpper < 0) {
                    inspectorIntArray = $"!!! (negative offset {longUpper})";
                    inspectorFloatArray = inspectorIntArray;
                    inspectorStringArray = inspectorIntArray;
                    inspectorRefArray = inspectorIntArray;
                }
                else if (longLower > 200) {
                    inspectorIntArray = $"!!! (array size implausibly big {longLower})";
                    inspectorFloatArray = inspectorIntArray;
                    inspectorStringArray = inspectorIntArray;
                    inspectorRefArray = inspectorIntArray;
                }
                else {
                    long lengthToEnd = varying.Length - longUpper;
                    if (lengthToEnd < longLower * 4) {
                        inspectorIntArray = $"!!! (no room for {longLower} ints)";
                        inspectorFloatArray = $"!!! (no room for {longLower} floats)";
                    }
                    else {
                        analysisIntArray = true;
                        analysisFloatArray = false;

                        StringBuilder sInt = new StringBuilder("[");
                        StringBuilder sFloat = new StringBuilder("[");
                        for (int i = 0; i < longLower; i++) {
                            sInt.Append(BitConverter.ToInt32(varying, (int)longUpper + 4 * i).ToString()); sInt.Append(", ");
                            float f = BitConverter.ToSingle(varying, (int)longUpper + 4 * i);
                            if (!AnalyseFloat(f)) analysisFloatArray = false;
                            sFloat.Append(f.ToString()); sFloat.Append(", ");
                        }
                        if (longLower > 0) {
                            sInt.Remove(sInt.Length - 2, 2);
                            sFloat.Remove(sFloat.Length - 2, 2);
                        }
                        sInt.Append(']');
                        sFloat.Append(']');
                        inspectorIntArray = sInt.ToString();
                        inspectorFloatArray = sFloat.ToString();
                    }

                    if (lengthToEnd < longLower * 8) {
                        inspectorStringArray = $"!!! (no room for {longLower} strings)";
                    }
                    else {
                        analysisStringArray = true;

                        StringBuilder s = new StringBuilder("[");
                        for (int i = 0; i < longLower; i++) {
                            long strOffset = BitConverter.ToInt64(varying, (int)longUpper + 8 * i);
                            if (strOffset < 0 || strOffset + 1 >= varying.Length) {
                                s.Append($"!!! (string oob {strOffset}, ");
                                analysisStringArray = false;
                            }
                            else {
                                s.Append(Dat.ReadWStringNullTerminated(varying, (int)strOffset));
                                s.Append(", ");
                            }
                        }

                        if (longLower > 0) {
                            s.Remove(s.Length - 2, 2);
                        }
                        s.Append(']');
                        inspectorStringArray = s.ToString();
                    }

                    if (lengthToEnd < longLower * 16) {
                        inspectorRefArray = $"!!! (no room for {longLower} refs)";
                    } else {
                        analysisRefArray = true;

                        StringBuilder s = new StringBuilder("[");
                        for (int i = 0; i < longLower; i++) {
                            long refLower = BitConverter.ToInt64(varying, (int)longUpper + 16 * i);
                            long refUpper = BitConverter.ToInt64(varying, (int)longUpper + 16 * i + 8);
                            if (refLower == -72340172838076674 && refUpper == -72340172838076674) {
                                s.Append("NULL, ");
                            } else if (refUpper != 0) {
                                analysisRefArray = false;
                                s.Append($"!!! (bytes 8-16 non zero {refUpper}), ");
                            } else if (refLower < 0) {
                                s.Append($"!!! (negative {refLower}), ");
                            } else if (refLower > maxRows) {
                                s.Append($"!!! (too large {refLower}), ");
                            } else {
                                s.Append($"{refLower}, ");
                            }
                        }
                        if (longLower > 0) {
                            s.Remove(s.Length - 2, 2);
                        }
                        s.Append(']');
                        inspectorRefArray = s.ToString();
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
                                if (tableColumns[i].type != Schema.Column.Type.Byte)
                                    Checkbox("##byte", ref columnByteMode[i]);
                                //SameLine();
                                TableHeader(columnName);
                                PopID();
                            }
                            //TableHeadersRow();
                            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                            clipper.Begin(dat.rowCount);
                            while (clipper.Step()) {
                                for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++) {
                                    TableNextRow();
                                    TableSetColumnIndex(0);
                                    Text(row.ToString()); //TODO garbagio
                                    for (int col = 0; col < tableColumns.Count; col++) {
                                        TableSetColumnIndex(col + 1);

                                        var column = tableColumns[col];

                                        if (column.type == Schema.Column.Type.rid)
                                            TableSetBgColor(ImGuiTableBgTarget.CellBg, GetColorU32(new System.Numerics.Vector4(0, 1, 0, 0.1f)));
                                        if (column.type == Schema.Column.Type.Byte || columnByteMode[col]) {
                                            ReadOnlySpan<char> text = rowBytes[row].AsSpan().Slice(column.offset * 3, column.Size() * 3 - 1);
                                            PushID(row * tableColumns.Count + col);
                                            if (Selectable(text, selectedColumn == col && selectedRow == row)) {

                                                selectedColumn = col;
                                                selectedRow = row;
                                                Analyse(row, column.offset);
                                            }
                                        } else {
                                            string text = columnData[col][row];
                                            PushID(row * tableColumns.Count + col);
                                            if (Selectable(text, selectedColumn == col && selectedRow == row)) {
                                                
                                                selectedColumn = col;
                                                selectedRow = row;
                                                Analyse(row, column.offset);
                                            }
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
                        InputTextMultiline("", ref currentSchemaText, 65535, new System.Numerics.Vector2(512, 1024));

                        if (Button("Parse")) {
                            UpdateSchema(currentSchemaText);
                        }
                        SameLine();
                        if (Button("Reset")) {
                            string fileTitleCase = lowercaseToTitleCaseDats[datFileList[datFileSelected]];
                            currentSchemaText = schemaText[fileTitleCase];
                        }
                    }

                    //INSPECTOR
                    Text("Inspector");
                    if(BeginTable("Inspector", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {
                        InspectorRow("Int", inspectorInt, analysisInt);
                        InspectorRow("Bool", inspectorBool, analysisBool);
                        InspectorRow("Float", inspectorFloat, analysisFloat);
                        InspectorRow("String", inspectorString, analysisString);
                        InspectorRow("Reference", inspectorRef, analysisRef);
                        InspectorRow("Int Array", inspectorIntArray, analysisIntArray);
                        InspectorRow("Float Array", inspectorFloatArray, analysisFloatArray);
                        InspectorRow("String Array", inspectorStringArray, analysisStringArray);
                        InspectorRow("Ref Array", inspectorRefArray, analysisRefArray);

                        EndTable();
                    }

                    //Text(selectedColumn.ToString());
                    //Text(selectedRow.ToString());
                }
                
                EndTable();
            }

        }

        void InspectorRow(string label, string value, bool analysis) {
            if (value == null) return;
            TableNextRow();
            if (!analysis) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
            TableSetColumnIndex(0); Text(label);
            TableSetColumnIndex(1); Text(value);
        }
    }
}
