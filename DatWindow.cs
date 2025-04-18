﻿using ImGuiNET;
using System;
using System.Text;
using static ImGuiNET.ImGui;
using PoeFormats;
using System.IO;
using System.Collections.Generic;
using System.Formats.Tar;

namespace ImGui.NET.SampleProgram {
    internal class DatWindow {
        string startupDat = "miscanimated";

        string datFolder = @"E:\Extracted\PathOfExile\3.25.SettlersPreorder\data";
        string schemaFolder;
        //string datFolder = @"F:\Extracted\PathOfExile\3.25.2\data";

        string failText = null;

        Schema schema;

        List<string> datFileList;
        Dictionary<string, int> datNameIndices;
        DatTab[] tabs;
        int selectedTab;

        int[] datRowCount;
        string[] datFileListSortedByRowCount;


        //dat analaysis
        Dictionary<string, DatMetadata> metadata;

        int maxRows = 0;

        int fileListMode = 0;
        int possibleRefMode = 0;

        bool hideZeroRowDats = true;
        string datFilter = "";

        string possibleRefFilter = "";
        string possibleRefValueFilter = "";

        string selectDat;
        int selectRow = -1;

        public DatWindow(string datFolder, string schemaFolder) {
            this.datFolder = datFolder;
            this.schemaFolder = schemaFolder;
            datFileList = new List<string>();
            datNameIndices = new Dictionary<string, int>();
            foreach(string datPath in Directory.EnumerateFiles(datFolder, "*.datc64")) {
                string datName = Path.GetFileNameWithoutExtension(datPath);
                datNameIndices[datName] = datFileList.Count;
                datFileList.Add(datName);
            }
            tabs = new DatTab[datFileList.Count];



            schema = new Schema(schemaFolder);

            datRowCount = new int[datFileList.Count];
            datFileListSortedByRowCount = new string[datFileList.Count];
            metadata = new Dictionary<string, DatMetadata>();
            for (int i = 0; i < datFileList.Count; i++) {
                
                string datFile = datFileList[i];
                if (!metadata.ContainsKey(datFile)) BuildMetadata(i);
            }

            foreach(string dat in metadata.Keys) {
                var meta = metadata[dat];
                if(meta.rowIds == null && schema.TryGetTable(dat, out var table)) {
                    for(int col = 0; col < table.columns.Length; col++) {
                        var column = table.columns[col];
                        if (column.type == Schema.Column.Type.rid && column.references != null && column.unique) {
                            Console.WriteLine($"{dat} -> {column.references} {column}");
                            string datPath = Path.Combine(datFolder, dat + ".datc64");
                            Dat d = new Dat(datPath);
                            meta.rowIds = d.Column(column, metadata[column.references.ToLower()].rowIds);
                        }
                    }
                }
            }

            Array.Sort<int, string>(datRowCount, datFileListSortedByRowCount);

            SelectDat(startupDat);
        }

        void BuildMetadata(int i) {
            string datFile = datFileList[i];
            datFileListSortedByRowCount[i] = datFile;
            string datPath = Path.Combine(datFolder, datFile + ".datc64");
            Dat d = new Dat(datPath);
            datRowCount[i] = d.rowCount;


            if (d.rowCount > maxRows) maxRows = d.rowCount;

            DatMetadata m = new DatMetadata { rowCount = d.rowCount, rowWidth = d.rowWidth, state = DatMetadata.State.Working };
            metadata[datFile] = m;

            if (schema.TryGetTable(datFile, out var table)) {

                var columns = table.columns;

                int columnsEnd = columns.Length > 0 ? columns[columns.Length - 1].offset + columns[columns.Length - 1].Size() : 0;
                int distToEnd = d.rowWidth - columnsEnd;
                if (distToEnd > 0) {
                    m.state = DatMetadata.State.ExtraData;
                } else if (distToEnd < 0) {
                    m.state = DatMetadata.State.MissingData;
                } else {
                    m.state = DatMetadata.State.Working;
                }


                //TODO better id detection
                if (columns.Length != 0 && columns[0].type == Schema.Column.Type.@string) {
                    m.rowIds = d.Column(columns[0]);
                } else if (columns.Length > 1 && columns[1].type == Schema.Column.Type.@string) {
                    m.rowIds = d.Column(columns[1]);
                }


                for (int col = 0; col < columns.Length; col++) {
                    var column = columns[col];
                    if(column.offset + column.Size() > d.rowWidth) {
                        m.state = DatMetadata.State.MissingData;
                        break;
                    }
                    int maxRef = 100000;
                    if (column.references != null) {
                        string refTable = column.references.ToLower();

                        if(refTable == datFile) {
                            maxRef = m.rowCount;
                        } else {
                            if (!datNameIndices.ContainsKey(refTable)) {
                                Console.WriteLine($"{refTable} MISSING!");
                                continue;
                            }
                            int refIndex = datNameIndices[refTable];

                            if (!metadata.ContainsKey(refTable)) {
                                BuildMetadata(refIndex);
                            }
                            maxRef = metadata[refTable].rowCount - 1;
                        }

                    }
                COULDNOTFINDDAT:
                    //TODO analyse only for specific type????
                    DatAnalysis.Error error = DatAnalysis.AnalyseColumn(d, column, maxRef);
                    if (error != DatAnalysis.Error.NONE) {
                        m.state = DatMetadata.State.Errors;
                        break;
                        //Console.WriteLine($"{datName} {column.TypeName()} AT {column.offset} {error}");
                    }
                }


            } else {
                m.state = DatMetadata.State.Undefined;
            }

        }
        

        void UpdateSchema(string text) {
            Schema.GqlReader r = new Schema.GqlReader(" " + text + " "); //TODO tokeniser doesn't work if start or end are proper characters lol
            schema.ParseGql(r);
            SelectDat(selectedTab, true); //TODO this is pretty wasteful
        }

        void SelectDat(int index, bool reload = false) {
            selectedTab = index;
            if (reload || tabs[selectedTab] == null) {
                tabs[selectedTab] = LoadDat(datFileList[index]);
            }
        }

        void SelectDat(string name, bool reload = false) {
            if (!datNameIndices.ContainsKey(name)) name = name.ToLower();
            selectedTab = datNameIndices[name];
            if (reload || tabs[selectedTab] == null) {
                tabs[selectedTab] = LoadDat(name);
            }
        }

        DatTab LoadDat(string filename) {
            if (schema.TryGetTable(filename, out var table)) {
                string datPath = Path.Combine(datFolder, filename.ToLower() + ".datc64");
                DatTab tab = new DatTab(datPath, table, metadata, maxRows);
                return tab;
            }
            return null;
        }


        public unsafe void Update(float width, float height) {
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
                            var dat = tabs[this.selectedTab];
                            dat.selectedRow = selectRow;
                        }
                    }
                }


                //DAT FILES
                float enumHeight = height - 64;
                TableSetColumnIndex(0);

                //RadioButton("All", ref fileListMode, 0); SameLine();
                //RadioButton("Tables", ref fileListMode, 1); SameLine();
                //RadioButton("Enums", ref fileListMode, 2); SameLine();
                //RadioButton("Undefined", ref fileListMode, 3);

                InputText("Filter", ref datFilter, 256);
                //Separator();

                if(BeginTable("DAT LIST TABLE", 1, ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(256, height - 80))) {

                    TableSetupColumn("file", ImGuiTableColumnFlags.WidthFixed, 256);

                    for (int i = 0; i < datFileList.Count; i++) {
                        string name = datFileList[i];
                        bool isSelected = this.selectedTab == i;

                        DatMetadata meta = metadata[name];
                        if (hideZeroRowDats && meta.rowCount == 0) continue;

                        if (datFilter.Length > 0 && !name.Contains(datFilter) && !isSelected) continue;
                        string tooltip = name + "\r\nnot defined";
                        bool isEnum = schema.TryGetEnum(datFileList[i], out var e);

                        if (schema.TryGetTable(datFileList[i], out var t)) {
                            if (fileListMode > 1 && t.columns.Length != 0) continue;
                            name = t.name;
                        } else if (isEnum) {
                            if (fileListMode % 2 == 1) continue;
                            name = e.name;
                        } else {
                            if (fileListMode == 1 || fileListMode == 2) continue;
                        }


                        var color = meta.state switch {
                            DatMetadata.State.Undefined => GetColorU32(new System.Numerics.Vector4(1, 1, 1, 0.2f)),
                            DatMetadata.State.ExtraData => GetColorU32(new System.Numerics.Vector4(1, 0.8f, 0.2f, 0.2f)),
                            DatMetadata.State.MissingData => GetColorU32(new System.Numerics.Vector4(0.1f, 0.6f, 1.0f, 0.2f)),
                            DatMetadata.State.Errors => GetColorU32(new System.Numerics.Vector4(1, 0.1f, 0.1f, 0.2f)),
                            DatMetadata.State.Working => GetColorU32(new System.Numerics.Vector4(0, 0, 0, 0.2f)),
                        };


                        TableNextRow();
                        TableSetColumnIndex(0);

                        TableSetBgColor(ImGuiTableBgTarget.CellBg, color);

                        //PushStyleColor(ImGuiCol.Header, color);
                        //PushStyleColor(ImGuiCol.HeaderActive, color);
                        //PushStyleColor(ImGuiCol.HeaderHovered, color);

                        if (Selectable(name, isSelected)) {
                            if (this.selectedTab != i) {
                                SelectDat(i);
                                selectedTab = true;
                            }
                            SetItemTooltip(tooltip);
                        }
                        //PopStyleColor();
                        //PopStyleColor();
                        //PopStyleColor();
                    }

                    EndTable();
                }
                Checkbox("Hide Empty", ref hideZeroRowDats);


                TableSetColumnIndex(1);
                if (BeginTabBar("DAT TAB BAR", ImGuiTabBarFlags.Reorderable)) {
                    for (int i = 0; i < datFileList.Count; i++) {
                        string name = datFileList[i];

                        var dat = tabs[i];
                        if (dat == null) continue;

                        bool open = true;
                        ImGuiTabItemFlags flags = selectedTab && this.selectedTab == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                        if (BeginTabItem(dat.table.name, ref open, flags)) {
                            if (!selectedTab && this.selectedTab != i) {
                                //SetTabItemClosed(dat.name);
                                this.selectedTab = i;
                            }
                            DatTable(dat);
                            EndTabItem();
                        }
                        if(!open) {
                            tabs[i] = null;
                            this.selectedTab = -1;
                        }
                    }
                    EndTabBar();
                }

                if (this.selectedTab >= 0 && tabs[this.selectedTab] != null) {
                    //DATA
                    TableSetColumnIndex(1);
                    if (failText == null) {

                    } else {
                        Text(failText);
                    }

                    //SCHEMA
                    TableSetColumnIndex(2);
                    DatInspector(tabs[this.selectedTab]);
                }
                
                EndTable();
            }
        }

        unsafe void DatTable(DatTab tab) {
            if (BeginTable(datFileList[selectedTab], tab.cols.Count + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable)) {
                TableSetupScrollFreeze(tab.cols.Count > 0 && tab.cols[0].column.name == "Id" ? 2 : 1, 1);

                TableSetupColumn("IDX");
                for (int i = 0; i < tab.cols.Count; i++) {
                    var column = tab.cols[i].column;
                    //TODO garbage
                    if (column.type == Schema.Column.Type.Byte) {
                        TableSetupColumn(column.offset.ToString(), ImGuiTableColumnFlags.NoResize, 14);
                    } else {
                        if (tab.cols[i].byteMode) {
                            TableSetupColumn($"{column.name}\n{column.TypeName()}\n{column.offset}", ImGuiTableColumnFlags.NoResize, column.Size() * 21 - 7);
                        } else {
                            TableSetupColumn($"{column.name}\n{column.TypeName()}\n{column.offset}");
                        }
                    }
                }
                TableNextRow();
                TableSetColumnIndex(0);
                TableHeader("Row");

                for (int i = 0; i < tab.cols.Count; i++) {
                    TableSetColumnIndex(i + 1);
                    string columnName = TableGetColumnName(i + 1);
                    PushID(i);
                    var column = tab.cols[i];

                    if (column.column.type != Schema.Column.Type.Byte)
                        Checkbox("##byte", ref column.byteMode);

                    bool error = column.error != DatAnalysis.Error.NONE;
                    if (error) PushStyleColor(ImGuiCol.TableHeaderBg, GetColorU32(new System.Numerics.Vector4(1, 0, 0, 0.2f)));
                    TableHeader(columnName);
                    if (error) PopStyleColor();
                    PopID();
                }
                //TableHeadersRow();
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(tab.dat.rowCount);
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
                        if (tab.selectedRow == row) TableSetBgColor(ImGuiTableBgTarget.RowBg0, GetColorU32(new System.Numerics.Vector4(0.3f, 0.5f, 1, 0.3f)));
                        Text(row.ToString()); //TODO garbagio
                        for (int col = 0; col < tab.cols.Count; col++) {
                            TableSetColumnIndex(col + 1);

                            var column = tab.cols[col];

                            if (column.column.type == Schema.Column.Type.rid)
                                TableSetBgColor(ImGuiTableBgTarget.CellBg, GetColorU32(new System.Numerics.Vector4(0, 1, 0, 0.1f)));
                            if (column.column.type == Schema.Column.Type.Byte || column.byteMode) {
                                ReadOnlySpan<char> text = tab.rowBytes[row].AsSpan().Slice(column.column.offset * 3, column.column.Size() * 3 - 1);
                                bool isZero = true;
                                for (int i = 0; i < column.column.Size(); i++)
                                    if (tab.dat.Row(row)[column.column.offset + i] != 0)
                                        isZero = false;
                                PushID(row * tab.cols.Count + col);
                                if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                if (Selectable(text, tab.selectedColumn == col && tab.selectedRow == row)) {
                                    tab.SelectColumn(col, maxRows);
                                    tab.selectedRow = row;
                                    tab.Analyse(row, column.column.offset);
                                }
                                if (isZero) PopStyleColor();
                                PopID();
                            } else {
                                string text = column.values[row];
                                bool isZero = text == "0" || text == "False";

                                PushID(row * tab.cols.Count + col);
                                if (isZero) PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                                if (Selectable(text, tab.selectedColumn == col && tab.selectedRow == row, ImGuiSelectableFlags.AllowDoubleClick)) {
                                    if (IsMouseDoubleClicked(0)) {
                                        if (column.column.type == Schema.Column.Type.rid && column.column.references != null) {
                                            selectDat = column.column.references;
                                            if(column.column.array) {
                                                if(tab.inspectorRefArrayValues.Count > 0) {
                                                    selectRow = tab.inspectorRefArrayValues[0];
                                                }
                                            } else {
                                                selectRow = tab.inspectorRefValue;
                                            }
                                        }
                                    } else {
                                        tab.SelectColumn(col, maxRows);
                                        tab.selectedRow = row;
                                        tab.Analyse(row, column.column.offset);
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


        unsafe void DatInspector(DatTab tab) {
            
            if (tab.schemaText != null) {
                InputTextMultiline("", ref tab.schemaText, 65535, new System.Numerics.Vector2(512, 768));

                if (Button("Parse")) {
                    UpdateSchema(tab.schemaText);
                }
                SameLine();
                if (Button("Reset")) {
                    //TODO fix reset
                    //string fileTitleCase = lowercaseToTitleCaseDats[datFileList[datFileSelected]];
                    //dat.schemaText = schemaText[fileTitleCase];
                }
                SameLine();
                if(Button("Export CSV")) {
                    string directory = @$"E:\Extracted\PathOfExile2\csv\{Path.GetFileName(Path.GetDirectoryName(datFolder))}\";
                    if(!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                    tab.ToCsv(directory + tab.table.name + ".csv");
                }
                if (Button("Export Column")) {
                    string directory = @$"E:\Extracted\PathOfExile2\csv\{Path.GetFileName(Path.GetDirectoryName(datFolder))}\";
                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                    tab.ColumnToTxt($"{directory}{tab.table.name}_{tab.selectedColumn}.txt", tab.selectedColumn);
                }
            }
            if (tab.selectedColumn != -1) {
                //INSPECTOR
                
                Text("Inspector");
                if (BeginTable("Inspector", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) {
                    InspectorRow("Ref Array", tab.inspectorRefArray, tab.selectedColumnAnalysis.isRefArray, Schema.Column.Type.rid, true);
                    InspectorRow("String Array", tab.inspectorStringArray, tab.selectedColumnAnalysis.isStringArray, Schema.Column.Type.@string, true);
                    InspectorRow("Float Array", tab.inspectorFloatArray, tab.selectedColumnAnalysis.isFloatArray, Schema.Column.Type.f32, true);
                    InspectorRow("Int Array", tab.inspectorIntArray, tab.selectedColumnAnalysis.isIntArray, Schema.Column.Type.i32, true);
                    InspectorRow("Unknown Array", tab.inspectorUnkArray, tab.selectedColumnAnalysis.isArray, Schema.Column.Type._, true);
                    InspectorRow("Reference", tab.inspectorRef, tab.selectedColumnAnalysis.isRef, Schema.Column.Type.rid);
                    InspectorRow("String", tab.inspectorString, tab.selectedColumnAnalysis.isString, Schema.Column.Type.@string);
                    InspectorRow("Float", tab.inspectorFloat, tab.selectedColumnAnalysis.isFloat, Schema.Column.Type.f32);
                    InspectorRow("Bool", tab.inspectorBool, tab.selectedColumnAnalysis.isBool, Schema.Column.Type.@bool);
                    InspectorRow("Hash16", tab.inspectorShort, tab.selectedColumnAnalysis.isHash16, Schema.Column.Type.u16);
                    InspectorRow("Int", tab.inspectorInt, tab.selectedColumnAnalysis.isInt, Schema.Column.Type.i32);
                    EndTable();
                }

                bool showRefValues = tab.selectedColumnAnalysis.isRef == DatAnalysis.Error.NONE && tab.inspectorRefValue >= 0;
                bool showRefArrayValues = tab.selectedColumnAnalysis.isRefArray == DatAnalysis.Error.NONE && tab.inspectorRefArrayValues != null && tab.inspectorRefArrayValues.Count > 0;
                if (showRefValues || showRefArrayValues) {

                    bool core = tab.table.file == "_Core";

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
                            if (datRowCount[i] > tab.selectedColumnAnalysis.maxRefArray) {
                                string tableName = datFileListSortedByRowCount[i]; string tableNameLower = tableName.ToLower();
                                if (possibleRefFilter.Length > 0 && !tableName.Contains(possibleRefFilter)) continue;

                                if(schema.TryGetTable(tableName, out var table)) {
                                    tableName = table.name;
                                    if (possibleRefMode == 4) continue;
                                    bool core = table.file == "_Core";
                                    bool sibling = table.file == tab.table.file;
                                    if (possibleRefMode == 1 && !sibling) continue;
                                    if (possibleRefMode == 2 && !core) continue;
                                    if (possibleRefMode == 3 && !sibling && !core) continue;
                                }

                                StringBuilder s = new StringBuilder("[");
                                for (int row = 0; row < tab.inspectorRefArrayValues.Count; row++) {
                                    if (metadata[tableNameLower].rowIds != null)
                                        s.Append(metadata[tableNameLower].rowIds[tab.inspectorRefArrayValues[row]]);
                                    else
                                        s.Append(tab.inspectorRefArrayValues[row].ToString());
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
                            if (datRowCount[i] > tab.selectedColumnAnalysis.maxRef) {
                                string tableName = datFileListSortedByRowCount[i]; string tableNameLower = tableName.ToLower();
                                if (possibleRefFilter.Length > 0 && !tableName.Contains(possibleRefFilter)) continue;

                                if (schema.TryGetTable(tableName, out var table)) {
                                    tableName = table.name;
                                    if (possibleRefMode == 4) continue;
                                    bool core = table.file == "_Core";
                                    bool sibling = table.file == tab.table.file;
                                    if (possibleRefMode == 1 && !sibling) continue;
                                    if (possibleRefMode == 2 && !core) continue;
                                    if (possibleRefMode == 3 && !sibling && !core) continue;
                                }

                                string s = metadata[tableNameLower].rowIds != null ? metadata[tableNameLower].rowIds[tab.inspectorRefValue] : tab.inspectorRefValue.ToString();

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
            DatTab tab = tabs[selectedTab];
            var column = tab.cols[tab.selectedColumn];
            var oldError = column.error;
            int offset = column.column.offset;

            var newColumn = new Schema.Column("_", type, offset, array);
            int newColumnEnd = offset + newColumn.Size();
            for(int i = tab.cols.Count - 1; i >= tab.selectedColumn; i--) {
                if (tab.cols[i].column.offset < newColumnEnd) {
                    tab.cols.RemoveAt(i);
                }
            }
            tab.cols.Insert(tab.selectedColumn, new DatTab.TableColumn() {
                column = newColumn,
                values = tab.dat.Column(newColumn, newColumn.references != null && metadata.ContainsKey(newColumn.references) ? metadata[newColumn.references].rowIds : null),
                error = DatAnalysis.AnalyseColumn(tab.dat, newColumn, maxRows),
                byteMode = false
            });
            if(tab.cols.Count > tab.selectedColumn + 1) {
                int nextOffset = tab.cols[tab.selectedColumn + 1].column.offset;
                for(int i = newColumnEnd; i < nextOffset; i++) {
                    tab.cols.Insert(tab.selectedColumn + 1, new DatTab.TableColumn() {
                        column = new Schema.Column(i),
                        values = null,
                        error = DatAnalysis.Error.NONE
                    });
                }
            }
            int lastDefined = tab.cols.Count - 1;
            while (lastDefined >= 0 && tab.cols[lastDefined].column.type == Schema.Column.Type.Byte) lastDefined--;

            Schema.Column[] newColumns = new Schema.Column[lastDefined + 1];
            for (int i = 0; i < newColumns.Length; i++) newColumns[i] = tab.cols[i].column;
            tab.schemaText = tab.table.ToGQL(newColumns, false);
        }
    }
}
