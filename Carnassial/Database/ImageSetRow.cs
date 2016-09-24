﻿using System;
using System.Collections.Generic;
using System.Data;

namespace Carnassial.Database
{
    public class ImageSetRow : DataRowBackedObject
    {
        public ImageSetRow(DataRow row)
            : base(row)
        {
        }

        public ImageFilter ImageFilter
        {
            get { return (ImageFilter)this.Row.GetIntegerField(Constants.DatabaseColumn.Filter); }
            set { this.Row.SetField(Constants.DatabaseColumn.Filter, (int)value); }
        }

        public int ImageRowIndex
        {
            get { return this.Row.GetIntegerField(Constants.DatabaseColumn.Row); }
            set { this.Row.SetField(Constants.DatabaseColumn.Row, value); }
        }

        public string Log
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.Log); }
            set { this.Row.SetField(Constants.DatabaseColumn.Log, value); }
        }

        public bool MagnifierEnabled
        {
            get { return this.Row.GetBooleanField(Constants.DatabaseColumn.Magnifier); }
            set { this.Row.SetField(Constants.DatabaseColumn.Magnifier, value); }
        }

        public string TimeZone
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.TimeZone); }
            set { this.Row.SetField(Constants.DatabaseColumn.TimeZone, value); }
        }

        public bool WhitespaceTrimmed
        {
            get { return this.Row.GetBooleanField(Constants.DatabaseColumn.WhiteSpaceTrimmed); }
            set { this.Row.SetField(Constants.DatabaseColumn.WhiteSpaceTrimmed, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Filter, (int)this.ImageFilter));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Log, this.Log));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Magnifier, this.MagnifierEnabled));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Row, this.ImageRowIndex));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.TimeZone, this.TimeZone));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, this.WhitespaceTrimmed));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public TimeZoneInfo GetTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }
    }
}