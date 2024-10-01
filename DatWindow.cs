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
        Dictionary<string, string> lowercaseToTitleCaseDats;

        Dat dat;


        string datName;
        string[] rows;
        List<Schema.Column> columnsICareAbout;
        List<string[]> columnData;

        string ToHexSpaced(byte[] b, int start = 0, int length = int.MaxValue) {
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
            LoadDat("monstertypes");

            //rows = new string[Math.Min(dat.rowCount, 10)];
            //for (int i = 0; i < rows.Length; i++) {
            //    rows[i] = ToHexSpaced(dat.rows[i]);
            //}
        }

        void LoadDat(string filename) {

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
                columnData.Add(dat.Column(col));
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
                TableSetColumnIndex(0);;
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
                    if (BeginTable("DATTABLE", columnsICareAbout.Count + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY)) {
                        TableSetupScrollFreeze(columnsICareAbout[0].name == "Id" ? 2 : 1, 1);

                        TableSetupColumn("IDX");
                        for (int i = 0; i < columnsICareAbout.Count; i++) {
                            var column = columnsICareAbout[i];
                            TableSetupColumn($"{column.name}\n{column.offset}");
                        }
                        TableHeadersRow();

                        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                        clipper.Begin(dat.rowCount);
                        while (clipper.Step()) {
                            for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++) {
                                TableNextRow();
                                TableSetColumnIndex(0);
                                Text(row.ToString()); //TODO garbagio
                                for (int col = 0; col < columnsICareAbout.Count; col++) {
                                    TableSetColumnIndex(col + 1);
                                    Text(columnData[col][row]);
                                }
                            }
                        }
                        EndTable();
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
                }
                
                EndTable();
            }

        }

    }
}
