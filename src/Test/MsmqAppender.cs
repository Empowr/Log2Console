#region Copyright & License
//
// Copyright 2001-2005 The Apache Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.IO;
using System.Messaging;
using System.Text;
using log4net.Core;

namespace SampleAppendersApp.Appender
{
    /// <summary>
    /// Appender writes to a Microsoft Message Queue
    /// </summary>
    /// <remarks>
    /// This appender sends log events via a specified MSMQ queue.
    /// The queue specified in the QueueName (e.g. .\Private$\log-test) must already exist on
    /// the source machine.
    /// The message label and body are rendered using separate layouts.
    /// </remarks>
    public class MsmqAppender : log4net.Appender.AppenderSkeleton
    {
        private MessageQueue _queue;
        private string _queueName;
        private log4net.Layout.PatternLayout _labelLayout;

        public MsmqAppender()
        {
        }

        public string QueueName
        {
            get { return _queueName; }
            set { _queueName = value; }
        }

        public log4net.Layout.PatternLayout LabelLayout
        {
            get { return _labelLayout; }
            set { _labelLayout = value; }
        }

        override protected void Append(LoggingEvent loggingEvent)
        {
            if (_queue == null)
            {
                if (MessageQueue.Exists(_queueName))
                {
                    _queue = new MessageQueue(_queueName);
                }
                else
                {
                    ErrorHandler.Error("Queue [" + _queueName + "] not found");
                }
            }

            if (_queue != null)
            {
                Message message = new Message
                {
                    Label = RenderLabel(loggingEvent)
                };

                using (var stream = new MemoryStream())
                {
                    var writer = new StreamWriter(stream, new UTF8Encoding(false, true));

                    RenderLoggingEvent(writer, loggingEvent);

                    writer.Flush();
                    stream.Position = 0;
                    message.BodyStream = stream;

                    _queue.Send(message);
                }
            }
        }

        private string RenderLabel(LoggingEvent loggingEvent)
        {
            if (_labelLayout == null)
            {
                return null;
            }

            using (var writer = new StringWriter())
            {
                _labelLayout.Format(writer, loggingEvent);
                return writer.ToString();
            }
        }
    }
}
