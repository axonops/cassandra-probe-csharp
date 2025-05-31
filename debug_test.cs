using CassandraProbe.Core.Models;
using CassandraProbe.Logging.Formatters;
using Newtonsoft.Json.Linq;

var session = new ProbeSession();
var json = JsonFormatter.FormatSession(session);
Console.WriteLine("Generated JSON:");
Console.WriteLine(json);
var parsed = JObject.Parse(json);
Console.WriteLine("\nTopology field type: " + parsed["topology"]?.Type);
Console.WriteLine("Topology field value: " + parsed["topology"]);
Console.WriteLine("Is null: " + (parsed["topology"]?.Type == JTokenType.Null));
EOF < /dev/null