using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ModTool.Exporting.Editor {
    public class FilteredEnumMaskField {
        public Type enumType {
            get { return _enumType; }
            set { _enumType = value; }
        }

        public int filter {
            get { return _filter; }
            set {
                _filter = value;
                Initialize();
            }
        }

        private Type _enumType;
        private int _filter;

        private string[] names;
        private int[] values;

        private string[] options;

        Dictionary<int, int> valueToMask;
        Dictionary<int, int> maskToValue;

        public FilteredEnumMaskField(Type enumType, int mask) {
            _enumType = enumType;
            _filter = mask;

            valueToMask = new Dictionary<int, int>();
            maskToValue = new Dictionary<int, int>();

            Initialize();
        }

        public int DoMaskField(string label, int value) {
            var mask = ValueToMask(value);

            mask = EditorGUILayout.MaskField(label, mask, options);

            return MaskToValue(mask);
        }

        private void Initialize() {
            valueToMask.Clear();
            maskToValue.Clear();

            var options = new List<string>();

            names = Enum.GetNames(enumType);
            values = (int[]) Enum.GetValues(enumType);

            var n = 0;

            for (var i = 0; i < values.Length; i++) {
                var value = values[i];

                if ((filter & value) != 0) {
                    valueToMask.Add(value, 1 << n);
                    maskToValue.Add(1 << n, value);

                    options.Add(names[i]);

                    n++;
                }
            }

            this.options = options.ToArray();
        }

        private int ValueToMask(int value) {
            var mask = 0;

            foreach (var valueMask in valueToMask) {
                if ((value & valueMask.Key) != 0)
                    mask |= valueMask.Value;
            }

            return mask;
        }

        private int MaskToValue(int mask) {
            var value = 0;

            foreach (var maskValue in maskToValue) {
                if ((mask & maskValue.Key) != 0)
                    value |= maskValue.Value;
            }

            return value;
        }
    }
}