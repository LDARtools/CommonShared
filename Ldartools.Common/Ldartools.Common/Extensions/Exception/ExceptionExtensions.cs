using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ldartools.Common.Extensions
{
    public static class ExceptionExtensions
    {
        public static Exception GetInnerMostException(this Exception exception, bool returnFirstAggregate = true)
        {
            var result = exception;
            while (result.InnerException != null)
            {
                result = result.InnerException;
            }

            if (result is AggregateException agEx && (agEx.InnerExceptions?.Count == 1 || returnFirstAggregate && agEx.InnerExceptions?.Count > 0))
            {
                result = agEx.InnerExceptions.First().GetInnerMostException(returnFirstAggregate);
            }
            return result;
        }

        public static string PrintFullStackTrace(this Exception exception)
        {
            var sb = new StringBuilder();

            using (var writer = new StringWriter(sb))
            {
                using (var indentedWriter = new IndentedTextWriter(writer, "    "))
                {
                    PrintFullStackTraceInternal(exception, indentedWriter);
                }
            }

            return sb.ToString();
        }

        private static void PrintFullStackTraceInternal(Exception exception, IndentedTextWriter writer)
        {
            writer.WriteLine(exception.Message);
            CopyWithIndent(exception.StackTrace, writer);

            writer.Indent++;
            if (exception is AggregateException aggregateException)
            {
                var i = 0;
                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    writer.WriteLine($"InnerException[{i++}]:");
                    PrintFullStackTraceInternal(innerException, writer);
                }

                if (aggregateException.InnerException != null && !aggregateException.InnerExceptions.Any(inner => ReferenceEquals(inner, aggregateException.InnerException)))
                {
                    writer.WriteLine("InnerException:");
                    PrintFullStackTraceInternal(aggregateException.InnerException, writer);
                }
            }
            else if(exception.InnerException != null)
            {
                writer.WriteLine("InnerException:");
                PrintFullStackTraceInternal(exception.InnerException, writer);
            }

            writer.Indent--;

        }

        private static void CopyWithIndent(string s, TextWriter writer)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            using(var reader = new StringReader(s))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    writer.WriteLine(line);
                }
            }
        }
    }
}
