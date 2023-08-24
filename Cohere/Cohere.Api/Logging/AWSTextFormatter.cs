﻿using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Api.Logging
{
    public class AWSTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            output.Write("Timestamp - {0} | Level - {1} | Message {2} {3}", logEvent.Timestamp, logEvent.Level, logEvent.MessageTemplate, output.NewLine);
            if (logEvent.Exception != null)
            {
                output.Write("Exception - {0}", logEvent.Exception);
            }
        }
    }
}
