﻿using System;

namespace Carnassial.Database
{
    public class FileTableFlagColumn : FileTableColumn
    {
        public FileTableFlagColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return String.Equals(value, Constants.Boolean.False, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, Constants.Boolean.True, StringComparison.OrdinalIgnoreCase);
        }
    }
}