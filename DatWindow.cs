using ImGuiNET;
using System;
using System.Text;
using static ImGuiNET.ImGui;
using PoeFormats;
using System.IO;
using System.Collections.Generic;

namespace ImGui.NET.SampleProgram {
    internal class DatWindow {

        string startupDat = "acts";

        string datFolder = @"E:\Extracted\PathOfExile\3.25.Settlers\data";
        string failText = null;

        Schema schema;

        List<string> datFileList;
        Dictionary<string, int> datNameIndices;
        DatTab[] dats;

        int[] datRowCount;
        string[] datFileListSortedByRowCount;

        int datFileSelected;



        //dat analaysis
        Dictionary<string, string[]> rowIds;
        int maxRows = 0;


        int possibleRefMode = 0;

        string possibleRefFilter = "";
        string possibleRefValueFilter = "";

        string selectDat;
        int selectRow = -1;

        public DatWindow(string path) {
            datFileList = new List<string>();
            datNameIndices = new Dictionary<string, int>();
            foreach(string datPath in Directory.EnumerateFiles(datFolder, "*.dat64")) {
                string datName = Path.GetFileNameWithoutExtension(datPath);
                datNameIndices[datName] = datFileList.Count;
                datFileList.Add(datName);
            }
            dats = new DatTab[datFileList.Count];



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

                if(schema.TryGetTable(datFile, out var table)) {
                    var columns = table.columns;
                    if (columns.Length != 0 && columns[0].type == Schema.Column.Type.@string) {
                        rowIds[table.name] = d.Column(columns[0]);
                    } else if (columns.Length > 1 && columns[1].type == Schema.Column.Type.@string) {
                        rowIds[table.name] = d.Column(columns[1]);
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
            SelectDat(datFileSelected, true); //TODO this is pretty wasteful
        }

        void SelectDat(int index, bool reload = false) {
            datFileSelected = index;
            if (reload || dats[datFileSelected] == null) {
                dats[datFileSelected] = LoadDat(datFileList[index]);
            }
        }

        void SelectDat(string name, bool reload = false) {
            if (!datNameIndices.ContainsKey(name)) name = name.ToLower();
            datFileSelected = datNameIndices[name];
            if (reload || dats[datFileSelected] == null) {
                dats[datFileSelected] = LoadDat(name);
            }
        }

        DatTab LoadDat(string filename) {
            if (schema.TryGetTable(filename, out var table)) {
                string datPath = Path.Combine(datFolder, filename.ToLower() + ".dat64");
                DatTab dat = new DatTab(datPath, table, rowIds, maxRows);
                return dat;
            }
            return null;
        }


        public unsafe void Update() {
            if (BeginTable("MAIN", 3)) {

                TableSetupColumn("Dat Files", ImGuiTableColumnFlags.WidthFixed, 256);
                TableSetupColumn("Data", ImGuiTableColumnFlags.WidthStretch);
                TableSetupColumn("Schema", ImGuiTableColumnFlags.WidthFixed, 512);
                TableHeadersRow();
                TableNextRow();

                //TODO can we close tab to fix flashing?
                bool selectedTab = false;
                if (selectDat != null) {
                    if (selectDat == "_SELECTED_") {
                        selectDat = null;
                    } else {
                        SelectDat(selectDat);
                        selectDat = "_SELECTED_";
                        selectedTab = true;
                        if (selectRow != -1) {
                            var dat = dats[datFileSelected];
                            dat.selectedRow = selectRow;
                        }
                    }
                }


                //DAT FILES
                TableSetColumnIndex(0);
                bool a = true;
                if (BeginListBox("##FILELIST", new System.Numerics.Vector2(256, 1024))) {
                    for (int i = 0; i < datFileList.Count; i++) { 
                        bool isSelected = datFileSelected == i;
                       
                        if (Selectable(datFileList[i], isSelected)) {
                            if(datFileSelected != i) {
                                SelectDat(i);
                                selectedTab = true;
                            }

                        }

                           
                    }
                    EndListBox();
                }

                TableSetColumnIndex(1);
                if (BeginTabBar("DAT TAB BAR", ImGuiTabBarFlags.Reorderable)) {
                    for (int i = 0; i < datFileList.Count; i++) {
                        var dat = dats[i];
                        if (dat == null) continue;

                        bool open = true;
                        ImGuiTabItemFlags flags = selectedTab && datFileSelected == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                        if (BeginTabItem(dat.table.name, ref open, flags)) {
                            if (!selectedTab && datFileSelected != i) {
                                //SetTabItemClosed(dat.name);
                                datFileSelected = i;
                            }
                            DatTable(dat);
                            EndTabItem();
                        }
                        if(!open) {
                            dats[i] = null;
                            datFileSelected = -1;
                        }
                    }

                    EndTabBar();
                }

                if(datFileSelected >= 0 && dats[datFileSelected] != null) {
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
            if (BeginTable(datFileList[datFileSelected], dat.cols.Count + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable)) {
                TableSetupScrollFreeze(dat.cols[0].column.name == "Id" ? 2 : 1, 1);

                TableSetupColumn("IDX");
                for (int i = 0; i < dat.cols.Count; i++) {
                    var column = dat.cols[i].column;
                    //TODO garbage
                    if (column.type == Schema.Column.Type.Byte) {
                        TableSetupColumn(column.offset.ToString(), ImGuiTableColumnFlags.NoResize, 14);
                    } else {
                        if (dat.cols[i].byteMode) {
                            TableSetupColumn($"{column.name}\n{column.TypeName()}\n{column.offset}", ImGuiTableColumnFlags.NoResize, column.Size() * 21 - 7);
                        } else {
                            TableSetupColumn($"{column.name}\n{column.TypeName()}\n{column.offset}");
                        }
                    }
                }
                TableNextRow();
                TableSetColumnIndex(0);
                TableHeader("Row");

                for (int i = 0; i < dat.cols.Count; i++) {
                    TableSetColumnIndex(i + 1);
                    string columnName = TableGetColumnName(i + 1);
                    PushID(i);
                    var column = dat.cols[i];

                    if (column.column.type != Schema.Column.Type.Byte)
                        Checkbox("##byte", ref column.byteMode);

                    bool error = column.analysis.GetError(column.column) != DatAnalysis.Error.NONE;
                    if (error) PushStyleColor(ImGuiCol.TableHeaderBg, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                    TableHeader(columnName);
                    if (error) PopStyleColor();
                    PopID();
                }
                //TableHeadersRow();
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(dat.dat.rowCount);
                if (selectRow != -1 && selectDat == null) {
                    clipper.IncludeItemByIndex(selectRow);
                }
                while (clipper.Step()) {
                    for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++) {
                        if(row == selectRow && selectDat == null) {
                            SetScrollHereY();
                            selectRow = -1;
                        }

                        TableNextRow();
                        TableSetColumnIndex(0);
                        if (dat.selectedRow == row) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(0.3f, 0.5f, 1, 0.3f)));
                        Text(row.ToString()); //TODO garbagio
                        for (int col = 0; col < dat.cols.Count; col++) {
                            TableSetColumnIndex(col + 1);

                            var column = dat.cols[col];

                            if (column.column.type == Schema.Column.Type.rid)
                                TableSetBgColor(ImGuiTableBgTarget.CellBg, GetColorU32(new System.Numerics.Vector4(0, 1, 0, 0.1f)));
                            if (column.column.type == Schema.Column.Type.Byte || column.byteMode) {
                                ReadOnlySpan<char> text = dat.rowBytes[row].AsSpan().Slice(column.column.offset * 3, column.column.Size() * 3 - 1);
                                bool isZero = true;
                                for (int i = 0; i < column.column.Size(); i++)
                                    if (dat.dat.Row(row)[column.column.offset + i] != 0)
                                        isZero = false;
                                PushID(row * dat.cols.Count + col);
                                if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                if (Selectable(text, dat.selectedColumn == col && dat.selectedRow == row)) {
                                    dat.selectedColumn = col;
                                    dat.selectedRow = row;
                                    dat.Analyse(row, column.column.offset);
                                }
                                if (isZero) PopStyleColor();
                                PopID();
                            } else {
                                string text = column.values[row];
                                bool isZero = text == "0" || text == "False";

                                PushID(row * dat.cols.Count + col);
                                if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                if (Selectable(text, dat.selectedColumn == col && dat.selectedRow == row, ImGuiSelectableFlags.AllowDoubleClick)) {
                                    if (IsMouseDoubleClicked(0)) {
                                        if (column.column.type == Schema.Column.Type.rid && column.column.references != null) {
                                            selectDat = column.column.references;
                                            if(column.column.array) {
                                                if(dat.inspectorRefArrayValues.Count > 0) {
                                                    selectRow = dat.inspectorRefArrayValues[0];
                                                }
                                            } else {
                                                selectRow = dat.inspectorRefValue;
                                            }


                                        }
                                    } else {
                                        dat.selectedColumn = col;
                                        dat.selectedRow = row;
                                        dat.Analyse(row, column.column.offset);
                                    }
                                }
                                if (isZero) PopStyleColor();
                                PopID();
                            }
                        }
                    }
                }

                clipper.End();
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
                    //TODO fix reset
                    //string fileTitleCase = lowercaseToTitleCaseDats[datFileList[datFileSelected]];
                    //dat.schemaText = schemaText[fileTitleCase];
                }
            }
            if (dat.selectedColumn != -1) {
                //INSPECTOR
                DatAnalysis tempAnalysis = dat.cols[dat.selectedColumn].analysis;
                Text("Inspector");
                if (BeginTable("Inspector", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {
                    InspectorRow("Ref Array", dat.inspectorRefArray, tempAnalysis.isRefArray, Schema.Column.Type.rid, true);
                    InspectorRow("String Array", dat.inspectorStringArray, tempAnalysis.isStringArray, Schema.Column.Type.@string, true);
                    InspectorRow("Float Array", dat.inspectorFloatArray, tempAnalysis.isFloatArray, Schema.Column.Type.f32, true);
                    InspectorRow("Int Array", dat.inspectorIntArray, tempAnalysis.isIntArray, Schema.Column.Type.i32, true);
                    InspectorRow("Unknown Array", dat.inspectorUnkArray, tempAnalysis.isArray, Schema.Column.Type._, true);
                    InspectorRow("Reference", dat.inspectorRef, tempAnalysis.isRef, Schema.Column.Type.rid);
                    InspectorRow("String", dat.inspectorString, tempAnalysis.isString, Schema.Column.Type.@string);
                    InspectorRow("Float", dat.inspectorFloat, tempAnalysis.isFloat, Schema.Column.Type.f32);
                    InspectorRow("Bool", dat.inspectorBool, tempAnalysis.isBool, Schema.Column.Type.@bool);
                    InspectorRow("Int", dat.inspectorInt, tempAnalysis.isInt, Schema.Column.Type.i32);
                    EndTable();
                }

                bool showRefValues = tempAnalysis.isRef == DatAnalysis.Error.NONE && dat.inspectorRefValue >= 0;
                bool showRefArrayValues = tempAnalysis.isRefArray == DatAnalysis.Error.NONE && dat.inspectorRefArrayValues != null && dat.inspectorRefArrayValues.Count > 0;
                if (showRefValues || showRefArrayValues) {

                    bool core = dat.table.file == "_Core";

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
                                string tableName = datFileListSortedByRowCount[i];
                                if (possibleRefFilter.Length > 0 && !tableName.Contains(possibleRefFilter)) continue;

                                if(schema.TryGetTable(tableName, out var table)) {
                                    tableName = table.name;
                                    if (possibleRefMode == 4) continue;
                                    bool core = table.file == "_Core";
                                    bool sibling = table.file == dat.table.file;
                                    if (possibleRefMode == 1 && !sibling) continue;
                                    if (possibleRefMode == 2 && !core) continue;
                                    if (possibleRefMode == 3 && !sibling && !core) continue;
                                }

                                StringBuilder s = new StringBuilder("[");
                                for (int row = 0; row < dat.inspectorRefArrayValues.Count; row++) {
                                    if (rowIds.ContainsKey(tableName))
                                        s.Append(rowIds[tableName][dat.inspectorRefArrayValues[row]]);
                                    else
                                        s.Append(dat.inspectorRefArrayValues[row].ToString());
                                    s.Append(", ");
                                }
                                if (s.Length > 1) s.Remove(s.Length - 2, 2); s.Append(']');
                                string ss = s.ToString();

                                if (possibleRefValueFilter.Length > 0 && !ss.ToLower().Contains(possibleRefValueFilter)) continue;

                                TableNextRow();
                                TableSetColumnIndex(0); Text($"{tableName} ({datRowCount[i]})");
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
                                string tableName = datFileListSortedByRowCount[i];
                                if (possibleRefFilter.Length > 0 && !tableName.Contains(possibleRefFilter)) continue;

                                if (schema.TryGetTable(tableName, out var table)) {
                                    tableName = table.name;
                                    if (possibleRefMode == 4) continue;
                                    bool core = table.file == "_Core";
                                    bool sibling = table.file == dat.table.file;
                                    if (possibleRefMode == 1 && !sibling) continue;
                                    if (possibleRefMode == 2 && !core) continue;
                                    if (possibleRefMode == 3 && !sibling && !core) continue;
                                }

                                string s = rowIds.ContainsKey(tableName) ? rowIds[tableName][dat.inspectorRefValue] : dat.inspectorRefValue.ToString();

                                if (possibleRefValueFilter.Length > 0 && !s.ToLower().Contains(possibleRefValueFilter)) continue;
                                TableNextRow();
                                TableSetColumnIndex(0); Text($"{tableName} ({datRowCount[i]})");
                                TableSetColumnIndex(1); Text(s); SetItemTooltip(s);


                            }
                            //string datFile
                        }
                        EndTable();
                    }
                }
            }
        }

        void InspectorRow(string label, string value, DatAnalysis.Error error, Schema.Column.Type columnType, bool array = false) {
            
            if (value == null) return;
            TableNextRow();
            bool analysis = error == DatAnalysis.Error.NONE;
            if (!analysis) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
            string tooltip = error.ToString();
            TableSetColumnIndex(0);
            if (!analysis) BeginDisabled();
            PushID(label);
            if(SmallButton("Add")) {
                //Console.WriteLine("TEXT");
                AddColumn(columnType, array);
            }
            PopID();
            if (!analysis) EndDisabled();
            TableSetColumnIndex(1); Text(label); SetItemTooltip(tooltip);
            TableSetColumnIndex(2); Text(value); SetItemTooltip(tooltip);
        }

        void AddColumn(Schema.Column.Type type, bool array) {
            DatTab dat = dats[datFileSelected];
            var column = dat.cols[dat.selectedColumn];
            var oldAnalysis = column.analysis;
            int offset = column.column.offset;

            var newColumn = new Schema.Column("_", type, offset, array);
            int newColumnEnd = offset + newColumn.Size();
            for(int i = dat.cols.Count - 1; i >= dat.selectedColumn; i--) {
                if (dat.cols[i].column.offset < newColumnEnd) {
                    dat.cols.RemoveAt(i);
                }
            }
            dat.cols.Insert(dat.selectedColumn, new DatTab.TableColumn() {
                column = newColumn,
                values = dat.dat.Column(newColumn, rowIds),
                analysis = oldAnalysis,
                byteMode = false
            });
            if(dat.cols.Count > dat.selectedColumn + 1) {
                int nextOffset = dat.cols[dat.selectedColumn + 1].column.offset;
                for(int i = newColumnEnd; i < nextOffset; i++) {
                    dat.cols.Insert(dat.selectedColumn + 1, new DatTab.TableColumn() {
                        column = new Schema.Column(i),
                        values = null,
                        analysis = new DatAnalysis(dat.dat, i, maxRows)
                    });
                }
            }

            Schema.Column[] newColumns = new Schema.Column[dat.cols.Count];
            for (int i = 0; i < dat.cols.Count; i++) newColumns[i] = dat.cols[i].column;
            dat.schemaText = dat.table.ToGQL(newColumns);
        }
    }
}
