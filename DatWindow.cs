using ImGuiNET;
using System;
using System.Text;
using static ImGuiNET.ImGui;
using PoeFormats;
using System.IO;
using System.Collections.Generic;
using Vortice.Win32;

namespace ImGui.NET.SampleProgram {
    internal class DatWindow {

        string datFolder = @"E:\Extracted\PathOfExile\3.25.Settlers\data";
        string failText = null;

        Schema schema;
        Dictionary<string, string> inputSchema;

        List<string> datFileList;
        int datFileSelected;

        Dictionary<string, string> schemaText;
        Dictionary<string, string> lowercaseToTitleCaseDats;

        Dat dat;

        //dat analaysis
        Dictionary<string, string[]> rowIds;
        int maxRows = 0;

        int selectedRow;
        int selectedColumn;


        string datName;
        string[] rows;
        List<Schema.Column> columnsICareAbout;
        List<string[]> columnData;
        string[] rowBytes;
        bool[] columnByteMode;

        bool byteView = false;

        //inspector results
        string inspectorBool; bool analysisBool;
        string inspectorInt; bool analysisInt;
        string inspectorFloat; bool analysisFloat;
        string inspectorString; bool analysisString;
        string inspectorRef; bool analysisRef;

        string inspectorIntArray;
        string inspectorFloatArray;
        string inspectorStringArray;
        string inspectorRefArray;

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
                failText = $"{filename} has no schema"; return;
            }
            string fileTitleCase = lowercaseToTitleCaseDats[filename];
            string text = schemaText[fileTitleCase];
            if (!text.StartsWith("type")) {
                failText = $"{filename} is not type"; return;
            }

            string datPath = Path.Combine(datFolder, filename + ".dat64");
            if (!File.Exists(datPath)) {
                failText = $"{datPath} does not exist"; return;
            }

            failText = null;
            dat = new Dat(datPath);
            datName = filename;
            columnData = new List<string[]>();
            columnsICareAbout = new List<Schema.Column>();

            Schema.Column[] columns = schema.schema[fileTitleCase];

            for (int i = 0; i < columns.Length; i++) {
                var col = columns[i];
                columnsICareAbout.Add(col);
                columnData.Add(dat.Column(col, rowIds));
            }
            columnByteMode = new bool[columnsICareAbout.Count];

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
        }

        void Analyse(int row, int columnOffset) {
            byte[] data = dat.data;
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
            analysisFloat = !(floatValue < 0.00001 && floatValue != 0);
            inspectorFloat = floatValue.ToString();

            analysisRef = false;
            long longLower = BitConverter.ToInt64(data, offset);
            long longUpper = BitConverter.ToInt64(data, offset + 8);
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

            analysisString = false;
            if(longLower < 0) {
                inspectorString = $"!!! (negative offset {longLower})";
            } else if(longLower + 1 >= dat.varying.Length) {
                    inspectorString = $"!!! (offset too big {longLower})";
            } else {
                analysisString = true;
                inspectorString = Dat.ReadWStringNullTerminated(dat.varying, (int)longLower);
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
                        if (BeginTable(datFileList[datFileSelected], columnsICareAbout.Count + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable)) {
                            TableSetupScrollFreeze(columnsICareAbout[0].name == "Id" ? 2 : 1, 1);

                            TableSetupColumn("IDX");
                            for (int i = 0; i < columnsICareAbout.Count; i++) {
                                var column = columnsICareAbout[i];
                                //TODO garbage
                                TableSetupColumn(column.array ? $"{column.name}\n[{column.type}]\n{column.offset}" : $"{column.name}\n{column.type}\n{column.offset}");
                            }
                            TableNextRow();
                            TableSetColumnIndex(0);
                            TableHeader("Row");

                            for (int i = 0; i < columnsICareAbout.Count; i++) {
                                TableSetColumnIndex(i + 1);
                                string columnName = TableGetColumnName(i + 1);
                                PushID(i);
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
                                    for (int col = 0; col < columnsICareAbout.Count; col++) {
                                        TableSetColumnIndex(col + 1);

                                        var column = columnsICareAbout[col];
                                        if (column.type == Schema.Column.Type.rid)
                                            TableSetBgColor(ImGuiTableBgTarget.CellBg, GetColorU32(new System.Numerics.Vector4(0, 1, 0, 0.1f)));
                                        if (columnByteMode[col]) {
                                            ReadOnlySpan<char> text = rowBytes[row].AsSpan().Slice(column.offset * 3, column.Size() * 3 - 1);
                                            Text(text);
                                        } else {
                                            string text = columnData[col][row];
                                            PushID(row * columnsICareAbout.Count + col);
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
                    string selectedDatName = datFileList[datFileSelected];
                    if (lowercaseToTitleCaseDats.ContainsKey(selectedDatName)) {
                        string text = schemaText[lowercaseToTitleCaseDats[selectedDatName]];
                        TableSetColumnIndex(2);
                        InputTextMultiline("", ref text, (uint)text.Length, new System.Numerics.Vector2(512, 1024));
                    }
                    if(BeginTable("Inspector", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {
                        if (inspectorInt != null) {
                            TableNextRow();
                            if (!analysisInt) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                            TableSetColumnIndex(0); Text("Int");
                            TableSetColumnIndex(1); Text(inspectorInt);
                        }
                        if (inspectorBool != null) {
                            TableNextRow();
                            if(!analysisBool) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                            TableSetColumnIndex(0); Text("Bool");
                            TableSetColumnIndex(1); Text(inspectorBool);
                        }
                        if (inspectorFloat != null) {
                            TableNextRow();
                            if (!analysisFloat) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                            TableSetColumnIndex(0); Text("Float");
                            TableSetColumnIndex(1); Text(inspectorFloat);
                        }
                        if (inspectorString != null) {
                            TableNextRow();
                            if (!analysisString) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                            TableSetColumnIndex(0); Text("String");
                            TableSetColumnIndex(1); Text(inspectorString);
                        }
                        if (inspectorRef != null) {
                            TableNextRow();
                            if (!analysisRef) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                            TableSetColumnIndex(0); Text("Ref");
                            TableSetColumnIndex(1); Text(inspectorRef);
                        }

                        EndTable();
                    }

                    Text(selectedColumn.ToString());
                    Text(selectedRow.ToString());
                }
                
                EndTable();
            }

        }

    }
}
