using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImGui.NET.SampleProgram {
    public class DatMetadata {
        public int rowCount;
        public int rowWidth;
        public string[] rowIds;

        public enum State {
            Undefined,
            ExtraData,
            MissingData,
            Errors,
            Working
        }

        public State state;
    }
}
