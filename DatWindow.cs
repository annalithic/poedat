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

        List<string> datFileList;
        Dictionary<string, int> datNameIndices;
        DatTab[] dats;

        int[] datRowCount;
        string[] datFileListSortedByRowCount;

        int datFileSelected;

        Dictionary<string, string> schemaText;

        Dictionary<string, string> lowercaseToTitleCaseDats;


        //dat analaysis
        Dictionary<string, string[]> rowIds;
        int maxRows = 0;


        int possibleRefMode = 0;

        string possibleRefFilter = "";
        string possibleRefValueFilter = "";

        public DatWindow(string path) {
            datFileList = new List<string>();
            datNameIndices = new Dictionary<string, int>();
            foreach(string datPath in Directory.EnumerateFiles(datFolder, "*.dat64")) {
                string datName = Path.GetFileNameWithoutExtension(datPath);
                datNameIndices[datName] = datFileList.Count;
                datFileList.Add(datName);
            }
            dats = new DatTab[datFileList.Count];


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

            SelectDat(startupDat);

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
            SelectDat(datFileList[datFileSelected], true); //TODO this is pretty wasteful
        }

        void SelectDat(string name, bool reload = false) {
            if (!datNameIndices.ContainsKey(name)) name = name.ToLower();
            datFileSelected = datNameIndices[name];
            if (reload || dats[datFileSelected] == null) {
                dats[datFileSelected] = LoadDat(name);
            }
        }

        DatTab LoadDat(string filename) {
            if (!schema.schema.ContainsKey(filename)) return null;

            if (datFileList[datFileSelected] != filename) {
                SelectDat(filename);
            }

            string tableName = lowercaseToTitleCaseDats.ContainsKey(filename) ? lowercaseToTitleCaseDats[filename] : filename;
            
            string tableSchema = schemaText.ContainsKey(tableName) ? schemaText[tableName] : "";

            string datPath = Path.Combine(datFolder, filename + ".dat64");
            //if (!File.Exists(datPath)) {
            //    failText = $"{datPath} does not exist"; return;
            //}
            DatTab dat = new DatTab(tableName, tableSchema, datPath, schema, rowIds, maxRows);
            return dat;
        }

        bool AnalyseFloat(float f) {
            if (f < 0.00001 && f > -0.00001 && f != 0) return false;
            if (f > 1000000000 || f < -1000000000) return false;
            return true;
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
                if (BeginListBox("##FILELIST", new System.Numerics.Vector2(256, 1024))) {
                    for (int i = 0; i < datFileList.Count; i++) { 
                        bool isSelected = datFileSelected == i;

                        if (Selectable(datFileList[i], isSelected)) {
                            if(datFileSelected != i) {
                                //datFileSelected = i;
                                SelectDat(datFileList[i]);
                            }

                        }

                           
                    }
                    EndListBox();
                }

                TableSetColumnIndex(1);
                if (BeginTabBar("DAT TAB BAR", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.AutoSelectNewTabs)) {
                    for(int i = 0; i < datFileList.Count; i++) {
                        var dat = dats[i];
                        if (dat == null) continue;

                        bool open = true;
                        if(BeginTabItem(dat.name, ref open)) {
                            if(datFileSelected != i) {
                                datFileSelected = i;
                            }
                            DatTable(dat);
                            EndTabItem();
                        }
                        if(!open) {
                            dats[i] = null;
                        }
                    }

                    EndTabBar();
                }

                if(dats[datFileSelected] != null) {
                    //DATA
                    TableSetColumnIndex(1);
                    if (failText == null) {

                    } else {
                        Text(failText);
                    }

                    //SCHEMA
                    TableSetColumnIndex(2);
                    DatInspector(dats[datFileSelected]);
                }
                
                EndTable();
            }

        }

        unsafe void DatTable(DatTab dat) {
            if (BeginTable(datFileList[datFileSelected], dat.tableColumns.Count + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable)) {
                TableSetupScrollFreeze(dat.tableColumns[0].name == "Id" ? 2 : 1, 1);

                TableSetupColumn("IDX");
                for (int i = 0; i < dat.tableColumns.Count; i++) {
                    var column = dat.tableColumns[i];
                    //TODO garbage
                    if (column.type == Schema.Column.Type.Byte) {
                        TableSetupColumn(column.offset.ToString());
                    } else {
                        TableSetupColumn($"{column.name}\n{column.TypeName()}\n{column.offset}");
                    }
                }
                TableNextRow();
                TableSetColumnIndex(0);
                TableHeader("Row");

                for (int i = 0; i < dat.tableColumns.Count; i++) {
                    TableSetColumnIndex(i + 1);
                    string columnName = TableGetColumnName(i + 1);
                    PushID(i);
                    var column = dat.tableColumns[i];

                    if (column.type != Schema.Column.Type.Byte)
                        Checkbox("##byte", ref dat.columnByteMode[i]);
                    //SameLine();
                    DatAnalysis an = dat.columnAnalysis[i];
                    bool error = an.GetError(column) != DatAnalysis.Error.NONE;

                    if (error) PushStyleColor(ImGuiCol.TableHeaderBg, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                    TableHeader(columnName);
                    if (error) PopStyleColor();
                    PopID();
                }
                //TableHeadersRow();
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(dat.dat.rowCount);
                while (clipper.Step()) {
                    for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++) {
                        TableNextRow();
                        TableSetColumnIndex(0);
                        if (dat.selectedRow == row) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(0.3f, 0.5f, 1, 0.3f)));
                        Text(row.ToString()); //TODO garbagio
                        for (int col = 0; col < dat.tableColumns.Count; col++) {
                            TableSetColumnIndex(col + 1);

                            var column = dat.tableColumns[col];

                            if (column.type == Schema.Column.Type.rid)
                                TableSetBgColor(ImGuiTableBgTarget.CellBg, GetColorU32(new System.Numerics.Vector4(0, 1, 0, 0.1f)));
                            if (column.type == Schema.Column.Type.Byte || dat.columnByteMode[col]) {
                                ReadOnlySpan<char> text = dat.rowBytes[row].AsSpan().Slice(column.offset * 3, column.Size() * 3 - 1);
                                bool isZero = true;
                                for (int i = 0; i < column.Size(); i++)
                                    if (dat.dat.Row(row)[column.offset + i] != 0)
                                        isZero = false;
                                PushID(row * dat.tableColumns.Count + col);
                                if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                if (Selectable(text, dat.selectedColumn == col && dat.selectedRow == row)) {

                                    dat.selectedColumn = col;
                                    dat.selectedRow = row;
                                    dat.Analyse(row, column.offset);
                                }
                                if (isZero) PopStyleColor();
                                PopID();
                            } else {
                                string text = dat.columnData[col][row];
                                bool isZero = text == "0" || text == "False";

                                PushID(row * dat.tableColumns.Count + col);
                                if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                if (Selectable(text, dat.selectedColumn == col && dat.selectedRow == row)) {

                                    dat.selectedColumn = col;
                                    dat.selectedRow = row;
                                    dat.Analyse(row, column.offset);
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


        unsafe void DatInspector(DatTab dat) {
            
            if (dat.schemaText != null) {
                InputTextMultiline("", ref dat.schemaText, 65535, new System.Numerics.Vector2(512, 768));

                if (Button("Parse")) {
                    UpdateSchema(dat.schemaText);
                }
                SameLine();
                if (Button("Reset")) {
                    string fileTitleCase = lowercaseToTitleCaseDats[datFileList[datFileSelected]];
                    dat.schemaText = schemaText[fileTitleCase];
                }
            }
            if (dat.selectedColumn != -1) {
                //INSPECTOR
                DatAnalysis tempAnalysis = dat.columnAnalysis[dat.selectedColumn];
                Text("Inspector");
                if (BeginTable("Inspector", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {

                    InspectorRow("Ref Array", dat.inspectorRefArray, tempAnalysis.isRefArray.ToString(), tempAnalysis.isRefArray == DatAnalysis.Error.NONE);
                    InspectorRow("String Array", dat.inspectorStringArray, tempAnalysis.isStringArray.ToString(), tempAnalysis.isStringArray == DatAnalysis.Error.NONE);
                    InspectorRow("Float Array", dat.inspectorFloatArray, tempAnalysis.isFloatArray.ToString(), tempAnalysis.isFloatArray == DatAnalysis.Error.NONE);
                    InspectorRow("Int Array", dat.inspectorIntArray, tempAnalysis.isIntArray.ToString(), tempAnalysis.isIntArray == DatAnalysis.Error.NONE);
                    InspectorRow("Unknown Array", dat.inspectorUnkArray, tempAnalysis.isArray.ToString(), tempAnalysis.isArray == DatAnalysis.Error.NONE);
                    InspectorRow("Reference", dat.inspectorRef, tempAnalysis.isRef.ToString(), tempAnalysis.isRef == DatAnalysis.Error.NONE);
                    InspectorRow("String", dat.inspectorString, tempAnalysis.isString.ToString(), tempAnalysis.isString == DatAnalysis.Error.NONE);
                    InspectorRow("Float", dat.inspectorFloat, tempAnalysis.isFloat.ToString(), tempAnalysis.isFloat == DatAnalysis.Error.NONE);
                    InspectorRow("Bool", dat.inspectorBool, tempAnalysis.isBool.ToString(), tempAnalysis.isBool == DatAnalysis.Error.NONE);
                    InspectorRow("Int", dat.inspectorInt, tempAnalysis.isInt.ToString(), tempAnalysis.isInt == DatAnalysis.Error.NONE);
                    EndTable();
                }

                bool showRefValues = tempAnalysis.isRef == DatAnalysis.Error.NONE && dat.inspectorRefValue >= 0;
                bool showRefArrayValues = tempAnalysis.isRefArray == DatAnalysis.Error.NONE && dat.inspectorRefArrayValues != null && dat.inspectorRefArrayValues.Count > 0;
                if (showRefValues || showRefArrayValues) {

                    bool core = schema.tableFiles[dat.name] == "_Core";

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
                                    bool sibling = file == schema.tableFiles[dat.name];
                                    if (possibleRefMode == 1 && !sibling) continue;
                                    if (possibleRefMode == 2 && !core) continue;
                                    if (possibleRefMode == 3 && !sibling && !core) continue;
                                }

                                StringBuilder s = new StringBuilder("[");
                                for (int row = 0; row < dat.inspectorRefArrayValues.Count; row++) {
                                    if (rowIds.ContainsKey(table))
                                        s.Append(rowIds[table][dat.inspectorRefArrayValues[row]]);
                                    else
                                        s.Append(dat.inspectorRefArrayValues[row].ToString());
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
                                    bool sibling = file == schema.tableFiles[dat.name];
                                    if (possibleRefMode == 1 && !sibling) continue;
                                    if (possibleRefMode == 2 && !core) continue;
                                    if (possibleRefMode == 3 && !sibling && !core) continue;
                                }
                                string s = rowIds.ContainsKey(table) ? rowIds[table][dat.inspectorRefValue] : dat.inspectorRefValue.ToString();

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
