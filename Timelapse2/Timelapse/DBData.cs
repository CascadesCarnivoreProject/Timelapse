﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;


namespace Timelapse
{
    public class DBData
    {
        #region Private Variables and Constants
        private SQLiteWrapper DB;      // A pointer to the Timelapse Data database
        #endregion 

        #region Public Properties

        public DataTable dataTable = new DataTable();        // contains the results of the data query
        public DataTable markerTable = new DataTable();      // contains the markers

        /// <summary>The complete path (excluding file name) of the data db. </summary>
        public string FolderPath { get; set; }

        /// <summary>The folder name (not the path) containing the data db </summary>
        public string Folder { get { return Utilities.GetFolderNameFromFolderPath(this.FolderPath); } }

        /// <summary>The file name of the data db lives. If accessed before it is set, it is set to the default data dbfilename value.</summary>
        private string pFilename = "";
        public string Filename
        {
            get
            {
                if (pFilename == "") pFilename = Constants.File.ImageDatabaseFileName;
                return pFilename;
            }
            set
            {
                pFilename = value;
            }
        }

        /// <summary>The path + file name of the data db. Set it to the default filename if its not already set. </summary>
        public string FilePath
        {
            get { return System.IO.Path.Combine(this.FolderPath, this.Filename); }
        }

        /// <summary>A pointer to the data table </summary>
        public DataTable templateTable { get; set; }

        /// <summary>A pointer to the table that has information about the entire image set</summary>
        public DataTable ImageSetTable { get; set; }

        /// <summary>The number of rows in the datatable, each row corresponding to an image </summary>
        public int ImageCount { get { return this.dataTable.Rows.Count; } }

        public int FindFile()
        {
            string[] files = System.IO.Directory.GetFiles(this.FolderPath, "*.ddb");
            if (files.Count() == 1)
            {
                this.Filename = System.IO.Path.GetFileName(files[0]); // Get the file name, excluding the path
                return 0;  // 0 means we have a valid .ddb filename
            }
            else if (files.Count() > 1)
            {
                DlgChooseDataBaseFile dlg = new DlgChooseDataBaseFile(files);
                bool? result = dlg.ShowDialog();
                if (result == true)
                {
                    this.Filename = dlg.selectedFile; // 0 means we have a valid .ddb filename
                    return 0;
                }
                else
                {
                    return 1; // User cancelled the file selection operation
                }

            }
            return 2; // There are no existing .ddb files files
        }
        /// <summary>Returns true if the data database exists</summary>
        public bool Exists { get { return File.Exists(this.FilePath); } }

        public int CurrentId = -1;                          // the Id of the current record. -1 if its not pointing to anything or if the database is empty
        public int CurrentRow = -1;

        // Access methods for the ImageDataTable key/values
        public string Log
        {
            set { this.UpdateRow(1, Constants.DatabaseElement.Log, value, Constants.Database.ImageSetTable); }
            get { return ImageSetGetValue(Constants.DatabaseElement.Log); }
        }

        public bool State_Magnifyer
        {
            set { this.UpdateRow(1, Constants.DatabaseElement.Magnifier, value.ToString(), Constants.Database.ImageSetTable); }
            get { string result = ImageSetGetValue(Constants.DatabaseElement.Magnifier); return Convert.ToBoolean(result); }
        }

        public int State_Row
        {
            set { this.UpdateRow(1, Constants.DatabaseElement.Row, value.ToString(), Constants.Database.ImageSetTable); }
            get { string result = ImageSetGetValue(Constants.DatabaseElement.Row); return Convert.ToInt32(result); }
        }

        public int State_Filter
        {
            set { int ifilter = (int)value; this.UpdateRow(1, Constants.DatabaseElement.Filter, ifilter.ToString(), Constants.Database.ImageSetTable); }
            get { string result = ImageSetGetValue(Constants.DatabaseElement.Filter); return Convert.ToInt32(result); }
        }

        public bool State_WhiteSpaceTrimmed
        {
            set { this.UpdateRow(1, Constants.DatabaseElement.WhiteSpaceTrimmed, value.ToString(), Constants.Database.ImageSetTable); }
            get
            {
                string result = ImageSetGetValue(Constants.DatabaseElement.WhiteSpaceTrimmed);
                return Convert.ToBoolean(result);
            }
        }
        public Hashtable DataLabelFromType = new Hashtable();
        public Hashtable TypeFromKey = new Hashtable();

        #endregion

        #region Constructors, Destructors

        /// <summary>Constructor </summary>
        public DBData() { }

        #endregion

        #region Public methods for creating the database and the lookup tables
        /// <summary>
        /// Create a database file (if needed) and connect to it. Also keeps a local copy of the template
        /// </summary>
        /// <returns></returns>
        public bool CreateDB(Template template)
        {
            // Create the DB
            try
            {
                this.DB = new SQLiteWrapper(this.FilePath);
                this.templateTable = template.templateTable;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        public void CreateTables()
        {

            // 1. Create the Data Table from the template
            // First, define the creation string based on the contents of the template. 
            Int64 id;
            string db_label;
            string default_value;
            bool result;
            string command_executed = "";
            Dictionary<string, string> column_definition = new Dictionary<string, string>();

            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // It begins with the ID integer primary key
            foreach (DataRow row in this.templateTable.Rows)
            {
                id = (Int64)row[Constants.Database.ID];
                db_label = (string)row[Constants.Control.DataLabel];
                default_value = (string)row[Constants.Control.DefaultValue];

                column_definition.Add(db_label, " Text '" + default_value + "'");
            }
            this.DB.CreateTable(Constants.Database.DataTable, column_definition, out result, out command_executed);
            // Debug.Print (result.ToString() + " " + command_executed);
            string command = "Select * FROM " + Constants.Database.DataTable;
            this.dataTable = this.DB.GetDataTableFromSelect(command, out result, out command_executed);


            //2. Create the ImageSetTable and initialize a single row in it
            column_definition.Clear();
            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // It begins with the ID integer primary key
            column_definition.Add(Constants.DatabaseElement.Log, " TEXT DEFAULT 'Add text here.'");

            column_definition.Add(Constants.DatabaseElement.Magnifier, " TEXT DEFAULT 'true'");
            column_definition.Add(Constants.DatabaseElement.Row, " TEXT DEFAULT '0'");
            int ifilter = (int)ImageQualityFilter.All;
            column_definition.Add(Constants.DatabaseElement.Filter, " TEXT DEFAULT '" + ifilter.ToString() + "'");
            this.DB.CreateTable(Constants.Database.ImageSetTable, column_definition, out result, out command_executed);

            Dictionary<String, String> dataline = new Dictionary<String, String>(); // Populate the data for the image set with defaults
            dataline.Add(Constants.DatabaseElement.Log, "Add text here");
            dataline.Add(Constants.DatabaseElement.Magnifier, "true");
            dataline.Add(Constants.DatabaseElement.Row, "0");
            dataline.Add(Constants.DatabaseElement.Filter, ifilter.ToString());
            List<Dictionary<string, string>> insertion_statements = new List<Dictionary<string, string>>();
            insertion_statements.Add(dataline);
            this.DB.InsertMultiplesBeginEnd(Constants.Database.ImageSetTable, insertion_statements, out result, out command_executed);

            // 3.Create the MarkersTable and initialize a single row in it
            column_definition.Clear();
            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // t begins with the ID integer primary key
            string type = "";
            foreach (DataRow row in this.templateTable.Rows)
            {
                type = (string)row[Constants.Database.Type];
                if (type.Equals(Constants.DatabaseElement.Counter))
                {
                    id = (Int64)row[Constants.Database.ID];
                    db_label = (string)row[Constants.Control.DataLabel];
                    string key = db_label;
                    column_definition.Add(db_label, " Text Default ''");
                }
            }
            this.DB.CreateTable(Constants.Database.MarkersTable, column_definition, out result, out command_executed);

            // 4. Copy the TemplateTable to this Database
            // First we have to create the table
            Dictionary<string, string> column_definitions = new Dictionary<string, string>();
            column_definitions.Add(Constants.Database.ID, "INTEGER primary key");
            column_definitions.Add(Constants.Database.ControlOrder, "INTEGER");
            column_definitions.Add(Constants.Database.SpreadsheetOrder, "INTEGER");
            column_definitions.Add(Constants.Database.Type, "text");
            column_definitions.Add(Constants.Control.DefaultValue, "text");
            column_definitions.Add(Constants.Control.Label, "text");
            column_definitions.Add(Constants.Control.DataLabel, "text");
            column_definitions.Add(Constants.Control.Tooltop, "text");
            column_definitions.Add(Constants.Control.TextBoxWidth, "text");
            column_definitions.Add(Constants.Control.Copyable, "text");
            column_definitions.Add(Constants.Control.Visible, "text");
            column_definitions.Add(Constants.Control.List, "text");
            this.DB.CreateTable(Constants.Database.TemplateTable, column_definitions, out result, out command_executed);

            DataView tempView = this.templateTable.DefaultView;
            tempView.Sort = "ID ASC";
            DataTable tempTable = tempView.ToTable();
            if (tempTable.Rows.Count != 0)
                this.DB.InsertDataTableIntoTable(Constants.Database.TemplateTable, tempTable, out result, out command_executed);
        }

        public void InitializeMarkerTableFromDataTable()
        {
            bool result;
            string command_executed = "";
            string command = "Select * FROM " + Constants.Database.MarkersTable;
            this.markerTable = this.DB.GetDataTableFromSelect(command, out result, out command_executed);
        }
        /// <summary>
        /// Create lookup tables that allow us to retrive a key from a type and vice versa
        /// TODO Should probably change this so its done internally rather than called externally
        /// </summary>
        public void CreateLookupTables()
        {
            Int64 id;
            string data_label;
            string rowtype;
            foreach (DataRow row in this.templateTable.Rows)
            {
                id = (Int64)row[Constants.Database.ID];
                data_label = (string)row[Constants.Control.DataLabel];
                rowtype = (string)row[Constants.Database.Type];

                // We don't want to add these types to the hash, as there can be multiple ones, which means the key would not be unique
                if (!(rowtype.Equals(Constants.DatabaseElement.Note) ||
                      rowtype.Equals(Constants.DatabaseElement.FixedChoice) ||
                      rowtype.Equals(Constants.DatabaseElement.Counter) ||
                      rowtype.Equals(Constants.DatabaseElement.Flag)))
                {
                    this.DataLabelFromType.Add(rowtype, data_label);
                }
                this.TypeFromKey.Add(data_label, rowtype);
            }
        }
        #endregion

        #region public methods for returning a copy of a table as is currently in the database
        public DataTable CreateDataTableFromDatabaseTable(string tablename)
        {
            bool result;
            string command_executed = "";
            string command = "Select * FROM " + tablename;
            return this.DB.GetDataTableFromSelect(command, out result, out command_executed);
        }
        #endregion

        #region Public methods for populating the Tabledata database with some basic filters
        /// <summary> 
        /// Populate the data datatable so that it matches all the entries in its associated database table.
        /// Then set the currentID and currentRow to the the first record in the returned set
        /// </summary>

        // Filter: All images
        public bool GetImagesAll()
        {
            return this.GetImages("*", "");
        }

        // Filter: Corrupted images only
        public bool GetImagesCorrupted()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Corrupted + "\"";  // = value

            return this.GetImages("*", where);
        }


        // Filter: Dark images only
        public bool GetImagesDark()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Dark + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Missing images only
        public bool GetImagesMissing()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Missing + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter:  images marked for deltion
        public bool GetImagesMarkedForDeletion()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.DeleteFlag]; // key
            where += "=\"true\""; // = value
            return this.GetImages("*", where);
        }

        public DataTable GetDataTableOfImagesMarkedForDeletion()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.DeleteFlag]; // key
            where += "=\"true\""; // = value
            return this.GetDataTableOfImages("*", where);
        }

        public bool GetImagesAllButDarkAndCorrupted()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += " IS NOT \"" + Constants.ImageQuality.Dark + "\"";  // = value
            where += " AND ";
            where += (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality];
            where += " IS NOT \"" + Constants.ImageQuality.Corrupted + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Custom filter - for a singe where Col=Value
        public bool GetImagesCustom(string datalabel, string comparison, string value)
        {
            string where = datalabel; // key
            where += comparison + "\"";
            where += value + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Custom Filter - for one or more Col=Value anded or or'd together
        public bool GetImagesCustom(string where_part)
        {
            // where should be of the form datalabel1=value1 AND datalabel2<>value2, etc. 
            return this.GetImages("*", where_part);
        }

        private bool GetImages(string searchstring, string where)
        {
            bool result;
            string command_executed;

            string query = "Select " + searchstring;
            query += " FROM " + Constants.Database.DataTable;
            if (!where.Equals("")) query += " WHERE " + where;
            // Debug.Print(query);
            DataTable tempTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            if (tempTable.Rows.Count == 0) return false;
            this.dataTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);

            return true;
        }


        private DataTable GetDataTableOfImages(string searchstring, string where)
        {
            bool result;
            string command_executed;

            string query = "Select " + searchstring;
            query += " FROM " + Constants.Database.DataTable;
            if (!where.Equals("")) query += " WHERE " + where;

            DataTable tempTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            if (tempTable.Rows.Count == 0) return null;
            tempTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            return tempTable;
        }

        public DataTable GetImagesAllForExporting()
        {
            bool result;
            string command_executed;

            string query = "Select * FROM " + Constants.Database.DataTable;
            return this.DB.GetDataTableFromSelect(query, out result, out command_executed);
        }
        #endregion

        #region Public methods for counting various things in the TableData
        public int[] GetImageCounts()
        {
            int[] counts = new int[4] { 0, 0, 0, 0 };
            counts[(int)ImageQualityFilter.Dark] = doCountQuery(Constants.ImageQuality.Dark);
            counts[(int)ImageQualityFilter.Corrupted] = doCountQuery(Constants.ImageQuality.Corrupted);
            counts[(int)ImageQualityFilter.Missing] = doCountQuery(Constants.ImageQuality.Missing);
            counts[(int)ImageQualityFilter.Ok] = doCountQuery(Constants.ImageQuality.Ok);
            return counts;
        }
        public int GetNoFilterCount()
        {
            return doCountQuery();
        }

        public int GetDeletedImagesCounts()
        {
            bool result;
            string command_executed = "";
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable + " Where " + (string)this.DataLabelFromType[Constants.DatabaseElement.DeleteFlag] + " = \"true\"";
                return this.DB.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        // helper method to the above that actually executes the query
        // This first form just returns the count of all images with no filters applied
        private int doCountQuery()
        {
            bool result;
            string command_executed = "";
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable;
                return this.DB.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        private int doCountQuery(string to_match)
        {
            bool result;
            string command_executed = "";
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable + " Where " + (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality] + " = \"" + to_match + "\"";
                return this.DB.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        public int GetCustomFilterCount(string where)
        {
            bool result;
            string command_executed = "";
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable + " Where " + where;
                return this.DB.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region Public methods for Inserting TableData Rows
        // Insert one or more rows into a table
        public void InsertMultipleRows(string table, List<Dictionary<String, String>> insertion_statements)
        {
            bool result;
            string command_executed = "";
            this.DB.InsertMultiplesBeginEnd(table, insertion_statements, out result, out command_executed);
        }
        #endregion 

        #region Public methods for Updating TableData Rows
        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        /// <param name="id"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void UpdateRow(int id, string key, string value)
        {
            UpdateRow(id, key, value, Constants.Database.DataTable);
        }
        public void UpdateRow(int id, string key, string value, string table)
        {
            bool result;
            string command_executed = "";
            String where = Constants.Database.ID + " = " + id.ToString();
            Dictionary<String, Object> dataline = new Dictionary<String, Object>(); // Populate the data 
            dataline.Add(key, value);
            this.DB.UpdateWhere(table, dataline, where, out result, out command_executed);

            //Update the datatable if that is the table currenty being considered.
            if (table.Equals(Constants.Database.DataTable))
            {
                //  NoT sure if this is more efficient than just looping through it, but...
                DataRow[] foundRows = this.dataTable.Select(Constants.Database.ID + " = " + id);
                if (foundRows.Length > 0)
                {
                    int index = this.dataTable.Rows.IndexOf(foundRows[0]);
                    this.dataTable.Rows[index][key] = (string)value;
                }
                //Debug.Print("In UpdateRow - Data: " + key + " " + value + " " + table);
            }
            else            //Update the MarkerTable if that is the table currenty being considered.
            {
                if (table.Equals(Constants.Database.MarkersTable))
                {
                    //  Not sure if this is more efficient than just looping through it, but...
                    DataRow[] foundRows = this.markerTable.Select(Constants.Database.ID + " = " + id);
                    if (foundRows.Length > 0)
                    {
                        int index = this.markerTable.Rows.IndexOf(foundRows[0]);
                        this.markerTable.Rows[index][key] = (string)value;
                    }
                    //Debug.Print("In UpdateRow -Marker: " + key + " " + value + " " + table);
                }
            }
        }

        // Update all rows across the entire database with the given key/value pair
        public void RowsUpdateAll(string key, string value)
        {
            bool result;
            string query = "Update " + Constants.Database.DataTable + " SET " + key + "=" + "'" + value + "'";
            this.DB.ExecuteNonQuery(query, out result);

            for (int i = 0; i < this.dataTable.Rows.Count; i++)
            {
                this.dataTable.Rows[i][key] = value;
            }
        }

        // SAUL: NEW TO TEST Update all rows across the entire database with the given key/value pair
        public void RowsUpdateMultipleRecordsByID(string key, string value)
        {
            bool result;
            string query = "Update " + Constants.Database.DataTable + " SET " + key + "=" + "'" + value + "'";
            this.DB.ExecuteNonQuery(query, out result);

            for (int i = 0; i < this.dataTable.Rows.Count; i++)
            {
                this.dataTable.Rows[i][key] = value;
            }
        }

        // Update all rows in the filtered view only with the given key/value pair
        public void RowsUpdateAllFilteredView(string key, string value)
        {
            bool result;
            string command_executed;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            for (int i = 0; i < this.dataTable.Rows.Count; i++)
            {
                this.dataTable.Rows[i][key] = value;
                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                id = (Int64)this.dataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + this.dataTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }
        // TODO: Change the date function to use this, as it currently updates the db one at a time.
        // This handy function efficiently updates multiple rows (each row identified by an ID) with different key/value pairs. 
        // For example, consider the tuple:
        // 5, myKey1, value1
        // 5, myKey2, value2
        // 6, myKey1, value3
        // 6, myKey2, value4
        // This will update;
        //     row with id 5 with value1 and value2 assigned to myKey1 and myKey2 
        //     row with id 6 with value3 and value3 assigned to myKey1 and myKey2 
        public void RowsUpdateByRowIdKeyVaue(List<Tuple<int, string, string>> dbupdate_list)
        {
            bool result;
            string command_executed = "";
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            string id;
            string key;
            string value;

            foreach (Tuple<int, string, string> t in dbupdate_list)
            {
                id = t.Item1.ToString();
                key = t.Item2;
                value = t.Item3;
                //Debug.Print(id.ToString() + " : " + key + " : " + value);

                DataRow datarow = this.dataTable.Rows.Find(id);
                datarow[key] = value;
                //Debug.Print(datarow.ToString());

                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                where = Constants.Database.ID + " = " + id;
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
            // Debug.Print(command_executed);
            //Debug.Print(result.ToString());
        }

        public void RowsUpdateFromRowToRow(string key, string value, int from, int to)
        {
            bool result;
            int from_id = from + 1; // rows start at 0, while indexes start at 1
            int to_id = to + 1;
            string query = "Update " + Constants.Database.DataTable + " SET " + key + "=" + "\"" + value + "\" ";
            query += "Where Id >= " + from_id.ToString() + " AND Id <= " + to_id.ToString();
            this.DB.ExecuteNonQuery(query, out result);

            for (int i = from; i <= to; i++)
            {
                this.dataTable.Rows[i][key] = value;
            }
        }

        // Given a list of column/value pairs (the String,Object) and the FILE name indicating a row, update it
        public void RowsUpdateRowsFromFilenames(Dictionary<Dictionary<String, Object>, String> update_query_list)
        {
            bool result;
            string command_executed;
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        public void RowsUpdateFromRowToRowFilteredView(string key, string value, int from, int to)
        {
            bool result;
            string command_executed;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            int from_id = from + 1; // rows start at 0, while indexes start at 1
            int to_id = to + 1;

            for (int i = from; i <= to; i++)
            {
                this.dataTable.Rows[i][key] = value;
                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                id = (Int64)this.dataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + this.dataTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        // Given a time difference in ticks, update all the date / time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever filter is being used..
        // TODO: modify this to include argments showing the current filtered view and row number, perhaps, so we could restore the datatable and the view?? 
        // TODO But that would add complications if there are unanticipated filtered views.
        // TODO: Another option is to go through whatever the current datatable is and just update those fields. 
        public void RowsUpdateAllDateTimeFieldsWithCorrectionValue(long ticks_difference, int from, int to)
        {
            bool result;
            string command_executed;
            string original_date = "";
            DateTime dtTemp;

            // We create a temporary table. We do this just in case we are currently on a filtered view
            DataTable tempTable = this.DB.GetDataTableFromSelect("Select * FROM " + Constants.Database.DataTable, out result, out command_executed);
            if (tempTable.Rows.Count == 0) return;

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            for (int i = from; i < to; i++)
            {
                original_date = (string)tempTable.Rows[i][Constants.DatabaseElement.Date] + " " + (string)tempTable.Rows[i][Constants.DatabaseElement.Time];
                result = DateTime.TryParse(original_date, out dtTemp);
                if (!result) continue; // Since we can't get a correct date/time, just leave it unaltered.

                // correct the date and modify the temporary datatable rows accordingly
                dtTemp = dtTemp.AddTicks(ticks_difference);
                tempTable.Rows[i][Constants.DatabaseElement.Date] = DateTimeHandler.StandardDateString(dtTemp);
                tempTable.Rows[i][Constants.DatabaseElement.Time] = DateTimeHandler.StandardTimeString(dtTemp);
            }

            // Now update the actual database with the new date/time values stored in the temporary table
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;
            for (int i = from; i < to; i++)
            {
                original_date = (string)tempTable.Rows[i][Constants.DatabaseElement.Date] + " " + (string)tempTable.Rows[i][Constants.DatabaseElement.Time];
                result = DateTime.TryParse(original_date, out dtTemp);
                if (!result) continue; // Since we can't get a correct date/time, don't create an update query for that row.

                columnname_value_list = new Dictionary<String, Object>();                       // Update the date and time
                columnname_value_list.Add(Constants.DatabaseElement.Date, tempTable.Rows[i][Constants.DatabaseElement.Date]);
                columnname_value_list.Add(Constants.DatabaseElement.Time, tempTable.Rows[i][Constants.DatabaseElement.Time]);
                id = (Int64)tempTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + tempTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void RowsUpdateSwapDayMonth()
        {
            bool result;
            string command_executed;
            string original_date = "";
            DateTime dtDate;
            DateTime reversedDate;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            if (this.dataTable.Rows.Count == 0) return;

            // Get the original date value of each. If we can swap the date order, do so. 
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                if (this.RowIsImageCorrupted(i)) continue;  // skip over corrupted images
                try
                {
                    // If we fail on any of these, continue on to the next date
                    original_date = (string)dataTable.Rows[i][Constants.DatabaseElement.Date];
                    dtDate = DateTime.Parse(original_date);
                    reversedDate = new DateTime(dtDate.Year, dtDate.Day, dtDate.Month); // we have swapped the day with the month
                }
                catch
                {
                    continue;
                };

                // Now update the actual database with the new date/time values stored in the temporary table
                columnname_value_list = new Dictionary<String, Object>();                  // Update the date 
                columnname_value_list.Add(Constants.DatabaseElement.Date, DateTimeHandler.StandardDateString(reversedDate));
                id = (Int64)this.dataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + id.ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        // It  assumes that the data table is showing All images
        public void RowsUpdateSwapDayMonth(int start, int end)
        {
            bool result;
            string command_executed;
            string original_date = "";
            DateTime dtDate;
            DateTime reversedDate;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            if (this.dataTable.Rows.Count == 0 || start >= this.dataTable.Rows.Count || end >= this.dataTable.Rows.Count) return;

            // Get the original date value of each. If we can swap the date order, do so. 
            for (int i = start; i <= end; i++)
            {
                if (this.RowIsImageCorrupted(i)) continue;  // skip over corrupted images
                try
                {
                    // If we fail on any of these, continue on to the next date
                    original_date = (string)dataTable.Rows[i][Constants.DatabaseElement.Date];
                    dtDate = DateTime.Parse(original_date);
                    reversedDate = new DateTime(dtDate.Year, dtDate.Day, dtDate.Month); // we have swapped the day with the month
                }
                catch
                {
                    continue;
                };

                // Now update the actual database with the new date/time values stored in the temporary table
                columnname_value_list = new Dictionary<String, Object>();                  // Update the date 
                columnname_value_list.Add(Constants.DatabaseElement.Date, DateTimeHandler.StandardDateString(reversedDate));
                id = (Int64)this.dataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + id.ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }
        #endregion

        #region Public methods for trimming white space in all table columns
        public void DataTableTrimDataWhiteSpace()
        {
            bool result;
            string command_executed;
            List<string> column_names = new List<string>();
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                DataRow row = templateTable.Rows[i];
                column_names.Add((string)row[Constants.Control.DataLabel]);
            }
            this.DB.UpdateColumnTrimWhiteSpace(Constants.Database.DataTable, column_names, out result, out command_executed);
        }
        #endregion

        #region Public Methods for Deleting Rows
        public void DeleteRow(int id)
        {
            bool result = true;
            string command_executed = "";
            this.DB.DeleteFromTable(Constants.Database.DataTable, "ID = " + id.ToString(), out result, out command_executed);
        }
        #endregion

        #region Public methods for Pragmas: Getting the schema
        // Not used
        public void GetTableSchema(string table)
        {
            bool result = false;
            string command_executed = "";
            //DataRow row;
            // DataColumn col;
            DataTable temp = this.DB.GetDataTableFromSelect("Pragma table_info('TemplateTable')", out result, out command_executed);
            if (result == false) return;
            for (int i = 0; i < temp.Rows.Count; i++)
            {
                string s = "====";
                for (int j = 0; j < temp.Columns.Count; j++)
                {
                    s += " " + temp.Rows[i][j].ToString();
                }
                Debug.Print(s);
            }
        }
        #endregion

        #region Public Methods related to Navigating TableData Rows
        /// <summary> 
        /// Return the Id of the current row 
        /// </summary>
        public int GetIdOfCurrentRow()
        {
            try
            {
                if (this.CurrentRow == -1) return -1;
                return Convert.ToInt32(this.dataTable.Rows[this.CurrentRow][Constants.Database.ID]); // The Id of the current row
            }
            catch  // If for some reason the above fails, we want to at least return something.
            {
                return -1;
            }
        }

        /// <summary> 
        // Go to the first row, returning true if we can otherwise false
        /// </summary>
        public bool ToDataRowFirst()
        {
            // Check if there are no rows. If none, set Id and Row indicators to reflect that
            if (this.dataTable.Rows.Count <= 0)
            {
                this.CurrentId = -1;
                this.CurrentRow = -1;
                return false;
            }

            // We have some rows. The first row is always 0, and then get the Id in that first row
            this.CurrentRow = 0; // The first row
            this.CurrentId = GetIdOfCurrentRow();
            return true;
        }

        /// <summary> 
        // Go to the next row, returning false if we can;t (e.g., if we are at the end) 
        /// </summary>
        public bool ToDataRowNext()
        {
            int count = this.dataTable.Rows.Count;

            // Check if we are on the last row. If so, do nothing and return false.
            if (this.CurrentRow >= (count - 1)) return false;

            // Go to the next row
            this.CurrentRow += 1;
            this.CurrentId = GetIdOfCurrentRow();
            return true;
        }

        /// <summary>
        /// Go to the previous row, returning true if we can otherwise false (e.g., if we are at the beginning)
        /// </summary>
        /// <returns></returns>
        public bool ToDataRowPrevious()
        {
            // Check if we are on the first row. If so, do nothing and return false.
            if (this.CurrentRow == 0) return false;

            // Go to the previous row
            this.CurrentRow -= 1;
            this.CurrentId = GetIdOfCurrentRow();
            return true;
        }

        /// <summary>
        /// Go to a particular row, returning true if we can otherwise false (e.g., if the index is out of range)
        /// Remember, that we are zero based, so (for example) and index of 5 will go to the 6th row
        /// </summary>
        /// <returns></returns>
        public bool ToDataRowIndex(int row_index)
        {
            int count = this.dataTable.Rows.Count;

            // Check if that particular row exists. If so, do nothing and return false.

            if (this.RowInBounds(row_index))
            {
                // Go to the previous row
                this.CurrentRow = row_index;
                this.CurrentId = GetIdOfCurrentRow();
                return true;
            }
            else return false;
        }
        #endregion

        #region Public Methods to get values from a TableData given an ID
        // Given a key and a data label, return its string value. Set result to succeeded or failed
        /// <summary>
        /// Example usage:
        ///   bool result;
        ///   string str = this.dbData.KeyGetValueFromDataLabel(3, Constants.FILE, out result);
        ///   MessageBox.Show(result.ToString() + " " + str);
        /// </summary>

        public string IDGetValueFromDataLabel(int key, string data_label, out bool result)
        {
            result = false;
            string found_string = "";
            DataRow foundRow = dataTable.Rows.Find(key);
            if (null != foundRow)
            {
                try
                {
                    found_string = (string)foundRow[data_label];
                    result = true;
                }
                catch { }
            }
            return found_string;
        }

        // Convenience functions for the standard data types, with the ID supplied
        public string IDGetFile(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.DatabaseElement.File, out result); }
        public string IDGetFolder(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.DatabaseElement.Folder, out result); }
        public string IDGetDate(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.DatabaseElement.Date, out result); }
        public string IDGetTime(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.DatabaseElement.Time, out result); }
        public string IDGetImageQuality(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.DatabaseElement.ImageQuality, out result); }

        // Convenience functions for the standard data types, where it assumes the Id is the currentID
        public string IDGetFile(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.File, out result); }
        public string IDGetFolder(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.Folder, out result); }
        public string IDGetDate(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.Date, out result); }
        public string IDGetTime(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.Time, out result); }
        public string IDGetImageQuality(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.ImageQuality, out result); }
        #endregion

        #region Public Methods to get values from a TableData given row

        public string RowGetValueFromType(string type)
        {
            return RowGetValueFromType(type, this.CurrentRow);
        }
        public string RowGetValueFromType(string type, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                string key = (string)this.DataLabelFromType[type];
                string result;
                try { result = (string)this.dataTable.Rows[row_index][key]; }
                catch { result = ""; }
                return result;
            }
            else return "";
        }

        // Given a row index, return the ID
        public int RowGetID(int row_index)
        {
            if (!this.RowInBounds(row_index)) return -1;
            try
            {
                Int64 id = (Int64)this.dataTable.Rows[row_index][Constants.Database.ID];
                return Convert.ToInt32(id);
            }
            catch
            {
                return -1;
            }
        }

        public string RowGetValueFromDataLabel(string data_label)
        {
            return RowGetValueFromDataLabel(data_label, this.CurrentRow);
        }
        public string RowGetValueFromDataLabel(string data_label, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                return (string)this.dataTable.Rows[row_index][data_label];
            }
            else return "";
        }


        // A convenience routine for checking to see if the image in the current row is displayable (i.e., not corrupted or missing)
        /// <summary> A convenience routine for checking to see if the image in the current row is displayable (i.e., not corrupted or missing) </summary>
        /// <returns></returns>
        public bool RowIsImageDisplayable()
        {
            return RowIsImageDisplayable(this.CurrentRow);
        }
        /// <summary> A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool RowIsImageDisplayable(int row)
        {
            string result = RowGetValueFromDataLabel((string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality], row);
            if (result.Equals(Constants.ImageQuality.Corrupted) || result.Equals(Constants.ImageQuality.Missing)) return false;
            return true;
        }

        /// <summary> A convenience routine for checking to see if the image in the given row is corrupted</summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool RowIsImageCorrupted(int row)
        {
            string result = RowGetValueFromDataLabel((string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality], row);
            return (result.Equals(Constants.ImageQuality.Corrupted)) ? true : false;
        }

        // Find the next displayable image after the provided row in the current image set
        public int RowFindNextDisplayableImage(int initial_row)
        {
            for (int row = initial_row; row < this.dataTable.Rows.Count; row++)
            {
                if (RowIsImageDisplayable(row)) return row;
            }
            return -1;
        }
        #endregion

        #region Public Methods to get values from a TemplateTable 

        public DataTable TemplateGetSortedByControls()
        {
            DataTable tempdt = this.templateTable.Copy();
            DataView dv = tempdt.DefaultView;
            dv.Sort = Constants.Database.ControlOrder + " ASC";
            return dv.ToTable();
        }

        public bool TemplateIsCopyable(string data_label)
        {
            bool is_copyable;
            int id = GetID(data_label);
            DataRow foundRow = templateTable.Rows.Find(id);
            return bool.TryParse((string)foundRow[Constants.Control.Copyable], out is_copyable) ? is_copyable : false;
            //id--;
            //return bool.TryParse((string)templateTable.Rows[id][Constants.COPYABLE], out is_copyable) ? is_copyable : false;
        }

        public string TemplateGetDefault(string data_label)
        {
            int id = GetID(data_label);
            DataRow foundRow = templateTable.Rows.Find(id);
            return (string)foundRow[Constants.Control.DefaultValue];
        }

        #endregion

        #region Public methods to set values in the TableData row
        public void RowSetValueFromDataLabel(string key, string value)
        {
            RowSetValueFromKey(key, value, this.CurrentRow);
        }
        private void RowSetValueFromKey(string key, string value, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                this.dataTable.Rows[this.CurrentRow][key] = value;
                this.UpdateRow(this.CurrentId, key, value);
            }
        }

        public void RowSetValueFromID(string key, string value, int id)
        {

            if (id == Convert.ToInt32(this.dataTable.Rows[this.CurrentRow][Constants.Database.ID].ToString()))
                this.dataTable.Rows[this.CurrentRow][key] = value;
            this.UpdateRow(id, key, value);
        }
        #endregion

        #region Public methods to get / set values in the ImageSetData

        /// <summary>Given a key, return its value</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string ImageSetGetValue(string key)
        {
            bool result;
            string command_executed = "";
            // Get the single row
            string query = "Select * From " + Constants.Database.ImageSetTable + " WHERE " + Constants.Database.ID + " = 1";
            DataTable imagesetTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            if (imagesetTable.Rows.Count == 0) return "";
            return (string)imagesetTable.Rows[0][key];
        }

        // Check if the White Space column exists in the ImageSetTable
        public bool DoesWhiteSpaceColumnExist()
        {
            return DB.isColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseElement.WhiteSpaceTrimmed);
        }

        // Create the White Space column exists in the ImageSetTable
        public bool CreateWhiteSpaceColumn()
        {
            if (DB.CreateColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseElement.WhiteSpaceTrimmed))
            {
                // Set the value to true 

                return true;
            }
            return false;
        }
        // <summary>Given a key, value pair, update its value</summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void ImageSetGetValue(string key, string value)
        {
            this.UpdateRow(1, Constants.DatabaseElement.Log, value, Constants.Database.ImageSetTable);
        }
        #endregion

        #region Public Methods to manipulate the MarkersTable

        /// <summary>
        /// Get the metatag counter list associated with all counters representing the current row
        /// It will have a MetaTagCounter for each control, even if there may be no metatags in it
        /// </summary>
        /// <returns>List<MetaTagCounter></returns>
        public List<MetaTagCounter> MarkerTableGetMetaTagCounterList()
        {
            List<MetaTagCounter> metaTagCounters = new List<MetaTagCounter>();

            // Test to see if we actually have a valid result
            if (this.markerTable.Rows.Count == 0) return metaTagCounters;    // This should not really happen, but just in case
            if (this.markerTable.Columns.Count == 0) return metaTagCounters; // Should also not happen as this wouldn't be called unless we have at least one counter control

            int id = this.GetIdOfCurrentRow();

            // Get the current row number of the id in the marker table
            int row_num = MarkerTableFindRowNumber(id);
            if (row_num < 0) return metaTagCounters;

            // Iterate through the columns, where we create a new MetaTagCounter for each control and add it to the MetaTagCounte rList
            MetaTagCounter mtagCounter;
            string datalabel = "";
            string value = "";
            List<Point> points;
            for (int i = 0; i < markerTable.Columns.Count; i++)
            {
                datalabel = markerTable.Columns[i].ColumnName;
                if (datalabel.Equals(Constants.Database.ID)) continue;  // Skip the ID

                // Create a new MetaTagCounter representing this control's meta tag,
                mtagCounter = new MetaTagCounter();
                mtagCounter.DataLabel = datalabel;

                // Now create a new Metatag for each point and add it to the counter
                try { value = (string)markerTable.Rows[row_num][datalabel]; } catch { value = ""; }
                points = this.ParseCoords(value); // parse the contents into a set of points
                foreach (Point point in points)
                {
                    mtagCounter.CreateMetaTag(point, datalabel);  // add the metatage to the list
                }
                metaTagCounters.Add(mtagCounter);   // and add that metaTag counter to our lists of metaTag counters
            }
            return metaTagCounters;
        }

        /// <summary>
        /// Given a point, add it to the Counter (identified by its datalabel) within the current row in the Marker table. 
        /// </summary>
        /// <param name="dataLabel"></param>
        /// <param name="point">A list of points in the form x,y|x,y|x,y</param>
        public void MarkerTableAddPoint(string dataLabel, string pointlist)
        {
            // Find the current row number
            int id = this.GetIdOfCurrentRow();
            int row_num = MarkerTableFindRowNumber(id);
            if (row_num < 0) return;

            // Update the database and datatable
            this.markerTable.Rows[row_num][dataLabel] = pointlist;
            this.UpdateRow(id, dataLabel, pointlist, Constants.Database.MarkersTable);  // Update the database
        }

        // Given a list of column/value pairs (the String,Object) and the FILE name indicating a row, update it
        public void RowsUpdateMarkerRows(Dictionary<Dictionary<String, Object>, String> update_query_list)
        {
            bool result;
            string command_executed;
            this.DB.UpdateWhereBeginEnd(Constants.Database.MarkersTable, update_query_list, out result, out command_executed);
        }
        public void RowsUpdateMarkerRows(List<ColumnTupleListWhere> update_query_list)
        {
            bool result;
            string command_executed;
            this.DB.UpdateWhereBeginEnd(Constants.Database.MarkersTable, update_query_list, out result, out command_executed);
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkersInRows(List<ColumnTupleListWhere> all_markers)
        {
            string sid;
            int id;
            char[] quote = { '\'' };
            foreach (ColumnTupleListWhere ctlw in all_markers)
            {
                ColumnTupleList ctl = ctlw.Listpair;
                // We have to parse the id, as its in the form of Id=5 (for example)
                sid = ctlw.Where.Substring(ctlw.Where.IndexOf("=") + 1);


                sid = sid.Trim(quote);
                if (!Int32.TryParse(sid, out id))
                {
                    Debug.Print("Can't GetThe Id");
                    break;
                }
                foreach (ColumnTuple ct in ctl)
                {
                    if (!ct.ColumnValue.Equals(""))
                    {
                        this.markerTable.Rows[id - 1][ct.ColumnName] = ct.ColumnValue;
                    }
                }
            }
        }

        /// <summary>
        /// Given an id, find the row number that matches it in the Marker Table
        /// </summary>
        /// <param name="id"></param>
        /// <returns>-1 on failure</returns>
        private int MarkerTableFindRowNumber(int id)
        {
            for (int row_number = 0; row_number < this.markerTable.Rows.Count; row_number++)
            {
                string str = markerTable.Rows[row_number][Constants.Database.ID].ToString();
                int this_id;
                if (Int32.TryParse(str, out this_id) == false) return -1;
                if (this_id == id) return row_number;
            }
            return -1;
        }

        private List<Point> ParseCoords(string value)
        {
            List<Point> points = new List<Point>();
            if (value.Equals("")) return points;

            char[] delimiterBar = { Constants.Database.MarkerBar };
            string[] sPoints = value.Split(delimiterBar);

            foreach (string s in sPoints)
            {
                Point point = Point.Parse(s);
                points.Add(point);
            }
            return points;
        }
        #endregion

        #region Private Methods

        private bool RowInBounds(int row_index)
        {
            return (row_index >= 0 && row_index < this.dataTable.Rows.Count) ? true : false;
        }

        private DataRow GetTemplateRow(string data_label)
        {
            int id = GetID(data_label);
            return templateTable.Rows.Find(id);
            // id--;
            //return this.templateTable.Rows[id];
        }

        /// <summary>Given a datalabel, get the id of the key's row</summary>
        /// <param name="data_label"></param>
        /// <returns></returns>
        private int GetID(string data_label)
        {
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                if (data_label.Equals(templateTable.Rows[i][Constants.Control.DataLabel]))
                {
                    Int64 id = (Int64)templateTable.Rows[i][Constants.Database.ID];
                    return Convert.ToInt32(id);
                }
            }
            return -1;
        }
        #endregion
    }

}
