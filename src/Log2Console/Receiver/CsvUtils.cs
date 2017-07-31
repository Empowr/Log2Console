using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Log2Console.Log;
using Log2Console.Settings;

namespace Log2Console.Receiver
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    [Serializable]
    [TypeConverterAttribute(typeof(ExpandableObjectConverter)),
    DescriptionAttribute("Expand to see the CSV options for the application.")]
    //[DisplayName("CSV Configuration")]
    public class CsvConfiguration
    {
        private FieldType[] _fieldList = new[]
                                             {
                                                 new FieldType(LogMessageField.SequenceNr, "sequence"),
                                                 new FieldType(LogMessageField.TimeStamp, "time"),
                                                 new FieldType(LogMessageField.Level, "level"),
                                                 new FieldType(LogMessageField.ThreadName, "thread"),
                                                 new FieldType(LogMessageField.CallSiteClass, "class"),
                                                 new FieldType(LogMessageField.CallSiteMethod, "method"),
                                                 new FieldType(LogMessageField.Message, "message"),
                                                 new FieldType(LogMessageField.Exception, "exception"),
                                                 new FieldType(LogMessageField.SourceFileName, "file")
                                             };

        [Category("Configuration")]
        [DisplayName("Field List")]
        [Description("Defines the type of each field")]
        public FieldType[] FieldList
        {
            get { return _fieldList; }
            set { _fieldList = value; }
        }

        private string _dateTimeFormat = "yyyy/MM/dd HH:mm:ss.fff";

        [Category("Configuration")]
        [DisplayName("Time Format")]
        [Description("Specifies the DateTime Format used to Parse the DateTime Field")]
        [DefaultValue("yyyy/MM/dd HH:mm:ss.fff")]
        public string DateTimeFormat
        {
            get { return _dateTimeFormat; }
            set { _dateTimeFormat = value; }
        }

        private string _quoteChar = "\"";

        [Category("Configuration")]
        [DisplayName("Quote Char")]
        [Description("If a field includes the delimiter, the whole field will be enclosed with a quote")]
        [DefaultValue("\"")]
        public string QuoteChar
        {
            get { return _quoteChar; }
            set { _quoteChar = value; }
        }

        private string _delimiter = ",";

        [Category("Configuration")]
        [DisplayName("Delimiter ")]
        [Description("The character used to delimit each field")]
        [DefaultValue(",")]
        public string Delimiter
        {
            get { return _delimiter; }
            set { _delimiter = value; }
        }

        [Category("Configuration")]
        [DisplayName("Read Header From File")]
        [Description("Read the Header or First List of the CSV File to Automatically determine the Field Types")]
        [DefaultValue(false)]
        public bool ReadHeaderFromFile { get; set; }
    }

    public class CsvUtils
    {
        public CsvConfiguration Config { get; set; }

        public List<LogMessage> ReadLogStream(StreamReader stream)
        {
            // Get last added lines
            var logMsgs = new List<LogMessage>();
            List<string> fields;

            while ((fields = ReadLogEntry(stream)).Count > 0)
            {
                LogMessage logMsg = ParseFields(fields);

                if (logMsg != null)
                    logMsgs.Add(logMsg);
            }
            return logMsgs;
        }

        private List<string> ReadLogEntry(StreamReader stream)
        {
            var finalFields = new List<string>();
            bool quoteDetected = false;
            StringBuilder quoteString = null;

            do
            {
                //If there is a log entry, that spans multiple lines, it will be surrounded by the Quote Character. 
                if (quoteDetected)
                {
                    quoteString.AppendLine();
                }

                var line = stream.ReadLine();

                if (string.IsNullOrEmpty(line)) //Skip blank lines
                {
                    if (quoteDetected)
                    {
                        quoteString.AppendLine();
                        continue;
                    }
                    return finalFields;
                }

                var fields = line.Split(new[] { Config.Delimiter }, StringSplitOptions.None);

                //Check if the Line is a header
                bool isHeaderLine = false;
                if (fields.Length == Config.FieldList.Length)
                {
                    isHeaderLine = true;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (!fields[i].Equals(Config.FieldList[i]))
                        {
                            isHeaderLine = false;
                            break;
                        }
                    }
                }
                if(isHeaderLine)
                    continue;

                //If not a header line, then continue to parse the line
                foreach (var nextField in fields)
                {
                    //First check for Quote Char in fields
                    if (!quoteDetected)
                    {
                        //See if there is a start quote
                        if ((nextField.Length > 0) && (nextField.Substring(0, 1).Equals(Config.QuoteChar)))
                        {
                            quoteString = new StringBuilder();
                            if ((nextField.Length > 1))
                            {
                                var fieldWithoutQuote = nextField.Substring(1, nextField.Length - 1);

                                //See if the last character is also a quote, in other words, see if the whole field is present in this field
                                if ((fieldWithoutQuote.Length > 0) && (fieldWithoutQuote.Substring(fieldWithoutQuote.Length - 1, 1).Equals(Config.QuoteChar)))
                                {
                                    fieldWithoutQuote = fieldWithoutQuote.Substring(0, nextField.Length - 1);
                                    finalFields.Add(fieldWithoutQuote);
                                }
                                else
                                {
                                    quoteString.Append(fieldWithoutQuote);
                                    quoteDetected = true;
                                }
                            }
                        }
                        //If not, simply add the field
                        else
                        {
                            finalFields.Add(nextField);
                        }
                    }
                    //Keep on concatenating the string until the end quote is detected
                    else
                    {
                        //See if the last character is a quote                        
                        if ((nextField.Length > 0) && (nextField.Substring(nextField.Length - 1, 1).Equals(Config.QuoteChar)))
                        {
                            var fieldWithoutQuote = nextField.Substring(0, nextField.Length - 1);
                            quoteString.Append(fieldWithoutQuote);
                            quoteDetected = false;
                            finalFields.Add(quoteString.ToString());
                        }
                        //No quote is detected, keep on adding the next field
                        else
                        {
                            quoteString.Append(nextField);

                            //Since this is enclosed in the Quote Char's it is part of a string field, and not valid delimiter
                            //Don't add a Delimiter if it is only one line
                            if(fields.Length > 1)
                                quoteString.Append(Config.Delimiter);  
                        }
                    }
                }

                //If this is a normal log entry, without any quotes, then check that the correct amount of fields is detected
                if (!quoteDetected && (finalFields.Count != Config.FieldList.Length))
                    return finalFields;

            } while (finalFields.Count < Config.FieldList.Length); //If this is a multi line log, keep on reading the following lines

            return finalFields;
        }

        private LogMessage ParseFields(List<string> fields)
        {
            var logMsg = new LogMessage { ThreadName = string.Empty };

            for (int i = 0; i < Config.FieldList.Length; i++)
            {               
                try
                {
                    var fieldType = Config.FieldList[i];
                    string fieldValue = fields[i];

                    if (fieldValue.Equals(fieldType.Name))
                    {
                        return null;
                    }

                    switch (fieldType.Field)
                    {
                        case LogMessageField.SequenceNr:                            
                            logMsg.SequenceNr = ulong.Parse(fieldValue);
                            break;
                        case LogMessageField.LoggerName:
                            logMsg.LoggerName = fieldValue;
                            break;
                        case LogMessageField.Level:
                            logMsg.Level = LogLevels.Instance[(LogLevel)Enum.Parse(typeof(LogLevel), fieldValue)];
                            //if (logMsg.Level == null)
                            //    throw new NullReferenceException("Cannot parse string: " + fieldValue);
                            break;
                        case LogMessageField.Message:
                            logMsg.Message = fieldValue;
                            break;
                        case LogMessageField.ThreadName:
                            logMsg.ThreadName = fieldValue;
                            break;
                        case LogMessageField.TimeStamp:
                            DateTime time;
                            DateTime.TryParseExact(fieldValue, Config.DateTimeFormat, null, DateTimeStyles.None, out time);
                            logMsg.TimeStamp = time;
                            break;
                        case LogMessageField.Exception:
                            logMsg.ExceptionString = fieldValue;
                            break;
                        case LogMessageField.CallSiteClass:
                            logMsg.CallSiteClass = fieldValue;
                            logMsg.LoggerName = logMsg.CallSiteClass;
                            break;
                        case LogMessageField.CallSiteMethod:
                            logMsg.CallSiteMethod = fieldValue;
                            break;
                        case LogMessageField.SourceFileName:
                            fieldValue = fieldValue.Trim("()".ToCharArray());
                            //Detect the Line Nr
                            var fileNameFields = fieldValue.Split(new[] { ":" }, StringSplitOptions.None);
                            if (fileNameFields.Length == 3)
                            {
                                uint line;
                                var lineNrString = fileNameFields[2];
                                if (uint.TryParse(lineNrString, out line))
                                    logMsg.SourceFileLineNr = line;

                                var fileName = fieldValue.Substring(0, fieldValue.Length - lineNrString.Length - 1);
                                logMsg.SourceFileName = fileName;
                            }
                            else
                                logMsg.SourceFileName = fieldValue;
                            break;
                        case LogMessageField.SourceFileLineNr:
                            logMsg.SourceFileLineNr = uint.Parse(fieldValue);
                            break;
                        case LogMessageField.Properties:
                            logMsg.Properties.Add(fieldType.Property, fieldValue);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    var sb = new StringBuilder();
                    foreach (var field in fields)
                    {
                        sb.Append(field);
                        sb.Append(Config.Delimiter);
                    }

                    logMsg = new LogMessage
                    {
                        SequenceNr = 0,
                        LoggerName = "Log2Console",
                        Level = LogLevels.Instance[LogLevel.Error],
                        Message = "Error Parsing Log Entry Line: " + sb,
                        ThreadName = string.Empty,
                        TimeStamp = DateTime.Now,
                        ExceptionString = ex.Message + ex.StackTrace,
                        CallSiteClass = string.Empty,
                        CallSiteMethod = string.Empty,
                        SourceFileName = string.Empty,
                        SourceFileLineNr = 0,
                    };
                    
                }
            }
            return logMsg;
        }

        public void AutoConfigureHeader(StreamReader stream)
        {
            if(stream == null)
                return;
            var line = stream.ReadLine();
            if(string.IsNullOrEmpty(line))
                return;
            
            var fields = line.Split(new[] { Config.Delimiter }, StringSplitOptions.None);
            bool headerValid = false;
            try
            {
                var fieldList = new FieldType[fields.Length];
                for (int index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];

                    if (UserSettings.Instance.CsvHeaderFieldTypes.ContainsKey(field))
                    {
                        fieldList[index] = UserSettings.Instance.CsvHeaderFieldTypes[field];

                        //Note: This is a very basic check for a valid header. If any field is detected, the header
                        //is considered valid. This could be made more thorough. 
                        headerValid = true;
                    }
                    else
                        fieldList[index] = new FieldType(LogMessageField.Properties, field, field);
                }

                if (headerValid)
                {
                    Config.FieldList = fieldList;
                }
                else
                {
                   //Todo: Add error notification here
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Could not Parse the Header: {0}\n\rError: {1}", line, ex),
                                "Error Parsing CSV Header");
            }
        }
    }
}
