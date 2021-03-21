using Carnassial.Util;
using System;
using System.Text;

namespace Carnassial.Database
{
    public class ColumnDefinition
    {
        public bool Autoincrement { get; set; }
        public string? DefaultValue { get; set; }
        public bool PrimaryKey { get; set; }
        public string Name { get; set; }
        public bool NotNull { get; set; }
        public string Type { get; set; }
        // support for Unique can be added if needed

        public ColumnDefinition(ColumnDefinition other)
        {
            this.Autoincrement = other.Autoincrement;
            this.DefaultValue = other.DefaultValue;
            this.Name = other.Name;
            this.NotNull = other.NotNull;
            this.PrimaryKey = other.PrimaryKey;
            this.Type = other.Type;
        }

        public ColumnDefinition(string name, DateTime defaultValue)
            : this(name, Constant.SQLiteAffinity.DateTime)
        {
            this.DefaultValue = DateTimeHandler.ToDatabaseDateTimeString(defaultValue);
            this.NotNull = true;
        }

        public ColumnDefinition(string name, string type)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }
            if (String.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            this.Autoincrement = false;
            this.DefaultValue = null;
            this.Name = name;
            this.NotNull = false;
            this.PrimaryKey = false;
            this.Type = type;
        }

        public ColumnDefinition(string name, TimeSpan defaultValue)
            : this(name, Constant.SQLiteAffinity.Real)
        {
            this.DefaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(defaultValue);
            this.NotNull = true;
        }

        public static ColumnDefinition CreateBoolean(string name)
        {
            return new ColumnDefinition(name, Constant.SQLiteAffinity.Integer)
            {
                DefaultValue = 0.ToString(Constant.InvariantCulture),
                NotNull = true
            };
        }

        public static ColumnDefinition CreatePrimaryKey()
        {
            return new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.SQLiteAffinity.Integer)
            {
                Autoincrement = true,
                PrimaryKey = true
            };
        }

        public override string ToString()
        {
            StringBuilder columnDefinition = new(SQLiteDatabase.QuoteIdentifier(this.Name) + " " + this.Type);
            if (this.DefaultValue != null)
            {
                if (String.Equals(this.Type, Constant.SQLiteAffinity.Text, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(this.Type, Constant.SQLiteAffinity.DateTime, StringComparison.OrdinalIgnoreCase))
                {
                    columnDefinition.Append(" DEFAULT " + SQLiteDatabase.QuoteStringLiteral(this.DefaultValue));
                }
                else
                {
                    columnDefinition.Append(" DEFAULT " + this.DefaultValue);
                }
            }
            if (this.NotNull)
            {
                columnDefinition.Append(" NOT NULL");
            }
            if (this.PrimaryKey)
            {
                columnDefinition.Append(" PRIMARY KEY");
                if (this.Autoincrement)
                {
                    columnDefinition.Append(" AUTOINCREMENT");
                }
            }

            return columnDefinition.ToString();
        }
    }
}
