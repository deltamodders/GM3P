using System;
using System.IO;
using System.Text;

namespace GM3P.Logging
{
    public class ConsoleLogger : IDisposable
    {
        private readonly FileStream? _fileStream;
        private readonly StreamWriter? _fileWriter;
        private readonly TextWriter? _doubleWriter;
        private readonly TextWriter _originalOut;

        public ConsoleLogger(string logPath)
        {
            _originalOut = Console.Out;

            try
            {
                _fileStream = File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                _fileWriter = new StreamWriter(_fileStream) { AutoFlush = true };
                _doubleWriter = new DoubleWriter(_fileWriter, _originalOut);
                Console.SetOut(_doubleWriter);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot open log file for writing: {e.Message}");
            }
        }

        public void Dispose()
        {
            Console.SetOut(_originalOut);
            _fileWriter?.Dispose();
            _fileStream?.Dispose();
        }

        private class DoubleWriter : TextWriter
        {
            private readonly TextWriter _first;
            private readonly TextWriter _second;

            public DoubleWriter(TextWriter first, TextWriter second)
            {
                _first = first;
                _second = second;
            }

            public override Encoding Encoding => _first.Encoding;

            public override void Flush()
            {
                _first.Flush();
                _second.Flush();
            }

            public override void Write(char value)
            {
                _first.Write(value);
                _second.Write(value);
            }

            public override void WriteLine(string? value)
            {
                _first.WriteLine(value);
                _second.WriteLine(value);
            }
        }
    }

    public static class ConsoleUtils
    {
        public static bool Confirm(string prompt)
        {
            ConsoleKey response;
            do
            {
                Console.Write($"{prompt} [y/n] ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                {
                    Console.WriteLine();
                }
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            return response == ConsoleKey.Y;
        }
    }
}