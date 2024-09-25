using ImGuiNET;
using System;
using System.Text;
using static ImGuiNET.ImGui;
using PoeFormats;
using System.IO;
using System.Collections.Generic;

namespace ImGui.NET.SampleProgram {
    internal class DatWindow {
        Schema schema;
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
            schema = new Schema(@"E:\Projects2\dat-schema\dat-schema");
            dat = new Dat(path);
            datName = Path.GetFileNameWithoutExtension(path);
            columnData = new List<string[]>();
            columnsICareAbout = new List<Schema.Column>();

            //TODO mega jank
            Schema.Column[] columns = null;
            foreach(string dat in schema.schema.Keys) {
                if(dat.ToLower() == datName) {
                    columns = schema.schema[dat];
                    break;
                }
            }

            for(int i = 0; i < columns.Length; i++) {
                var col = columns[i];
                columnsICareAbout.Add(col);
                columnData.Add(dat.Column(col));
            }

            //rows = new string[Math.Min(dat.rowCount, 10)];
            //for (int i = 0; i < rows.Length; i++) {
            //    rows[i] = ToHexSpaced(dat.rows[i]);
            //}
        }
        public unsafe void Update() {

            if (BeginTable("MAIN", 2)) {
                TableNextRow();
                TableSetColumnIndex(0);;
                if (BeginListBox("##FILELIST")) {
                    Text("AAAAAAAAAAA");
                    Text("BBBBBBBBBBB");
                    Text("CCCCCCCCCCC");
                    EndListBox();
                }


                TableSetColumnIndex(1);
                //Text("B");
                if (BeginTable("DATTABLE", columnsICareAbout.Count + 1, ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY)) {
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
                EndTable();
            }

        }

    }
}
