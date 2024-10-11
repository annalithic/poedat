using PoeFormats;
using System;

namespace ImGui.NET.SampleProgram {
    struct Analysis {
        [Flags] public enum Error : ushort {
            NONE = 0,

            OOB = 1,

            VALUE_TOO_SMALL = 2,
            VALUE_TOO_BIG = 4,

            COUNT_TOO_SMALL = 8,
            COUNT_TOO_BIG = 16,

            OFFSET_TOO_SMALL = 32,
            OFFSET_TOO_BIG = 64,

            UPPER_BYTES_NOT_ZERO = 128,
            OFFSET_NOT_EVEN = 256,
        }

        public Error isBool;
        public Error isInt;
        public Error isFloat;
        public Error isString;
        public Error isRef;

        public Error isArray;
        public Error isIntArray;
        public Error isFloatArray;
        public Error isStringArray;
        public Error isRefArray;

        public int maxRef;
        public int maxRefArray;

        public Analysis(Dat dat, int columnOffset, int maxRows) {
            for (int i = 0; i < dat.rowCount; i++) {
                Analyse(dat, columnOffset, i, maxRows);
            }
        }

        static Error AnalyseFloat(float f) {
            if (f < 0.00001 && f > -0.00001 && f != 0) 
                return Error.VALUE_TOO_SMALL;
            if (f > 1000000000 || f < -1000000000) 
                return Error.VALUE_TOO_BIG;
            return Error.NONE;
        }

        static Error AnalyseString(byte[] offsetLocation, byte[] varying, int offset) {
            long stringOffset = BitConverter.ToInt64(offsetLocation, offset);
            if (stringOffset < 8)
                return Error.OFFSET_TOO_SMALL;
            if (stringOffset + 1 >= varying.Length)
                return Error.OFFSET_TOO_BIG;
            if (stringOffset % 2 == 1)
                return Error.OFFSET_NOT_EVEN;
            return Error.NONE;
        }

        static Error AnalyseRef(long lower, long upper, int maxRows) {
            if (upper == -72340172838076674 && lower == -72340172838076674)
                return Error.NONE;
            if (upper != 0)
                return Error.UPPER_BYTES_NOT_ZERO;
            if (lower < 0)
                return Error.VALUE_TOO_SMALL;
            if (lower > maxRows)
                return Error.VALUE_TOO_BIG;
            return Error.NONE;
        }

        void Analyse(Dat dat, int columnOffset, int row, int maxRows) {
            byte[] data = dat.data;
            byte[] varying = dat.varying;
            int rowWidth = dat.rowWidth;
            int distToEnd = rowWidth - columnOffset;
            int offset = rowWidth * row + columnOffset;

            if (distToEnd <= 0) {
                isBool = Error.OOB;
            } else if (data[offset] > 1) {
                isBool = isBool | Error.VALUE_TOO_BIG;
            }

            if (distToEnd < 4) {
                isInt = Error.OOB;
                isFloat = Error.OOB;
            } else { 
                isFloat = isFloat | AnalyseFloat(BitConverter.ToSingle(data, offset));
            }

            if (distToEnd < 8) {
                isString = Error.OOB;
            } else {
                isString = isString | AnalyseString(data, varying, offset);
            }

            if (distToEnd < 16) {
                isRef = Error.OOB;
                isArray = Error.OOB;
            } else {
                long arrayCount = BitConverter.ToInt64(data, offset);
                long arrayOffset = BitConverter.ToInt64(data, offset + 8);
                int intArrayOffset = (int)arrayOffset;

                Error refResult = AnalyseRef(arrayCount, arrayOffset, maxRows);
                isRef = isRef | refResult;
                if (refResult == Error.NONE && arrayCount > maxRef)
                    maxRef = (int)arrayCount;


                //arrays
                if (arrayCount < 0)
                    isArray = isArray | Error.COUNT_TOO_SMALL;
                else if (arrayCount > 200)
                    isArray = isArray | Error.COUNT_TOO_BIG;
                else if (arrayOffset < 0)
                    isArray = isArray | Error.OFFSET_TOO_SMALL;
                else if (arrayCount > 0) {
                    int arrayDistToEnd = varying.Length - (int)arrayOffset;
                    if(arrayDistToEnd <= 0) 
                        isArray = isArray | Error.OFFSET_TOO_BIG;
                    
                    if(arrayDistToEnd < arrayCount * 4) {
                        isIntArray = isIntArray | Error.OFFSET_TOO_BIG;
                        isFloatArray = isFloatArray | Error.OFFSET_TOO_BIG;
                    } else {
                        for(int i = 0; i < arrayCount; i++) {
                            isFloatArray = isFloatArray | AnalyseFloat(BitConverter.ToSingle(varying, intArrayOffset + i * 4));
                        }
                    }

                    if(arrayDistToEnd < arrayCount * 8) {
                        isStringArray = isStringArray | Error.OFFSET_TOO_BIG;
                    } else {
                        for (int i = 0; i < arrayCount; i++) {
                            isStringArray = isStringArray | AnalyseString(varying, varying, intArrayOffset + i * 8);
                        }
                    }

                    if(arrayDistToEnd < arrayCount * 16) {
                        isRefArray = isRefArray | Error.OFFSET_TOO_BIG;
                    } else {
                        for (int i = 0; i < arrayCount; i++) {
                            long lower = BitConverter.ToInt64(varying, intArrayOffset);
                            long upper = BitConverter.ToInt64(varying, intArrayOffset + 8);

                            Error refArrayResult = AnalyseRef(lower, upper, maxRows);
                            isRefArray = isRefArray | refArrayResult;
                            if (refArrayResult == Error.NONE && lower > maxRefArray)
                                maxRefArray = (int)lower;
                        }
                    }
                }
            }

            isIntArray = isIntArray | isArray;
            isFloatArray = isFloatArray | isArray;
            isStringArray = isStringArray | isArray;
            isRefArray = isRefArray | isArray;
        }
        
    }


}
