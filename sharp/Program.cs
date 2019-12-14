﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Olympus {
    public class Program {

        public static void Main(string[] args) {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Process parentProc = null;

            if (args.Length == 0) {
                Console.WriteLine(@"""no parent pid""");
                Console.WriteLine(@"null");
                Console.Out.Flush();
                // return;

            } else {
                try {
                    parentProc = Process.GetProcessById(int.Parse(args[0]));
                } catch {
                    Console.WriteLine(@"null");
                    Console.WriteLine(@"{""error"": ""invalid parent id""}\n");
                    Console.Out.Flush();
                    return;
                }

                bool debug = false;

                for (int i = 1; i < args.Length; i++) {
                    string arg = args[i];
                    if (arg == "--debug") {
                        debug = true;
                    }
                }

                if (debug) {
                    Debugger.Launch();
                    Console.WriteLine(@"""debug""");

                } else {
                    Console.WriteLine(@"""ok""");
                }

                Console.WriteLine(@"null");
                Console.Out.Flush();
            }

            if (parentProc != null) {
                Thread killswitch = new Thread(() => {
                    try {
                        while (!parentProc.HasExited) {
                            Thread.Yield();
                            Thread.Sleep(1000);
                        }
                        Environment.Exit(0);
                    } catch {
                        Environment.Exit(-1);
                    }
                }) {
                    Name = "Killswitch",
                    IsBackground = true
                };
                killswitch.Start();
            }

            Cmds.Init();

            JsonSerializer serializer = new JsonSerializer();

            while (!(parentProc?.HasExited ?? false)) {
                using (JsonTextWriter writer = new JsonTextWriter(Console.Out)) {
                    try {
                        using (JsonTextReader reader = new JsonTextReader(Console.In) {
                            SupportMultipleContent = true
                        }) {
                            while (!(parentProc?.HasExited ?? false)) {
                                // Commands from Olympus come in pairs of two objects:

                                // Command ID
                                reader.Read();
                                string id = serializer.Deserialize<string>(reader).ToLowerInvariant();
                                Cmd cmd = Cmds.Get(id);
                                if (cmd == null) {
                                    reader.Read();
                                    reader.Skip();
                                    Console.WriteLine(@"null");
                                    writer.WriteStartObject();
                                    writer.WritePropertyName("error");
                                    writer.WriteValue("cmd failed running: not found: " + id);
                                    writer.WriteEndObject();
                                    writer.Flush();
                                    Console.WriteLine();
                                    Console.Out.Flush();
                                    continue;
                                }

                                // Payload
                                reader.Read();
                                object input = serializer.Deserialize(reader, cmd.InputType);
                                object output;
                                try {
                                    output = cmd.Run(input);

                                } catch (Exception e) {
                                    Console.WriteLine(@"null");
                                    writer.WriteStartObject();
                                    writer.WritePropertyName("error");
                                    writer.WriteValue("cmd failed running: " + e);
                                    writer.WriteEndObject();
                                    writer.Flush();
                                    Console.WriteLine();
                                    Console.Out.Flush();
                                    continue;
                                }

                                serializer.Serialize(writer, output, cmd.OutputType);
                                writer.Flush();
                                Console.WriteLine();
                                Console.WriteLine(@"null");
                                Console.Out.Flush();
                            }
                        }

                    } catch (Exception e) {
                        Console.WriteLine(@"null");
                        writer.WriteStartObject();
                        writer.WritePropertyName("error");
                        writer.WriteValue("cmd failed parsing: " + e);
                        writer.WriteEndObject();
                        writer.Flush();
                        Console.WriteLine();
                        Console.Out.Flush();
                    }
                }
            }
        }

    }
}
