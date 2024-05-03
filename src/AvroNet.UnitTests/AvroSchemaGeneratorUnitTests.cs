using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = AvroNet.UnitTests.Verifiers.CSharpSourceGeneratorVerifier<AvroNet.AvroGenerator>;

namespace AvroNet.UnitTests;

public class AvroSchemaGeneratorUnitTests
{
    private const string Attribute = $$""""
        {{AvroGenerator.AutoGeneratedComment}}
        namespace AvroNet
        {
            [global::System.CodeDom.Compiler.GeneratedCodeAttribute("AvroNet", "1.0.0.0")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false)]
            internal sealed class AvroModelAttribute : global::System.Attribute { }
        }
        """";

    [Fact]
    public async Task Generator_NoCandidates_AddAttributeUnconditionally()
    {
        string code = """
            namespace Tests
            {
                public sealed class Program
                {
                    public static void Main(string[] args)
                    {
                    }
                }
            }
            """;

        await VerifyCS.VerifyGeneratorAsync(code, [], ("AvroModelAttribute.g.cs", Attribute));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task Generator_WithCandidates_AddRecord()
    {
        string code = """"
            using System;
            using AvroNet;

            namespace Tests.OpenClass;
            
            [AvroModel]
            public partial record class User
            {
                public const string SchemaJson = """
                {
                   "type": "record",
                   "namespace": "Tests.User",
                   "name": "User",
                   "fields" : [
                      { "name": "Name", "type": "string" },
                      { "name": "Age", "type": "int", "default": 18 },
                      { "name": "Description", "type": [ "string", "null" ] }
                   ]
                }
                """;
            }
            """";
#pragma warning disable format
        string user = """"
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            #nullable enable
            namespace Tests.OpenClass;

            [global::System.CodeDom.Compiler.GeneratedCodeAttribute("AvroNet", "1.0.0.0")]
            public partial record class User : global::{|#0:Avro|}.Specific.ISpecificRecord
            {
                public static readonly global::{|#1:Avro|}.Schema _SCHEMA = global::{|#2:Avro|}.Schema.Parse(SchemaJson);
                public global::{|#3:Avro|}.Schema Schema { get => User._SCHEMA; }
                public required string Name { get; init; }
                public required int Age { get; init; } = 18;
                public string? Description { get; init; }
                
                public object? Get(int fieldPos)
                {
                    switch (fieldPos)
                    {
                        case 0: return this.Name;
                        case 1: return this.Age;
                        case 2: return this.Description;
                        default: throw new global::{|#4:Avro|}.AvroRuntimeException($"Bad index {fieldPos} in Get()");
                    }
                }

                public void Put(int fieldPos, object? fieldValue)
                {
                    switch (fieldPos)
                    {
                        case 0: Set_Name(this, (string)fieldValue!); break;
                        case 1: Set_Age(this, (int)fieldValue!); break;
                        case 2: Set_Description(this, (string?)fieldValue!); break;
                        default: throw new global::{|#5:Avro|}.AvroRuntimeException($"Bad index {fieldPos} in Put()");
                    }

                    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Name")]
                    extern static string Set_Name(User obj, string value);
                    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Age")]
                    extern static int Set_Age(User obj, int value);
                    [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Description")]
                    extern static string? Set_Description(User obj, string? value);
                }

            }

            #nullable restore
            
            """";
#pragma warning restore format

        DiagnosticResult[] diagnostics = [
            ..Enumerable.Range(0, 5).Select(i => DiagnosticResult.CompilerError("CS0400").WithLocation(i))
        ];

        await VerifyCS.VerifyGeneratorAsync(code, diagnostics,
            ("AvroModelAttribute.g.cs", Attribute),
            ("User.AvroModel.g.cs", user));
    }
#else
    [Fact]
    public async Task Generator_WithCandidates_AddRecord()
    {
        string code = """"
            using System;
            using AvroNet;

            namespace Tests.OpenClass
            {
                [AvroModel]
                public partial class User
                {
                    public const string SchemaJson = @"
            {
                ""type"": ""record"",
                ""namespace"": ""Tests.User"",
                ""name"": ""User"",
                ""fields"" : [
                    { ""name"": ""Name"", ""type"": ""string"" },
                    { ""name"": ""Age"", ""type"": ""int"", ""default"": 18 },
                    { ""name"": ""Description"", ""type"": [ ""string"", ""null"" ] }
                ]
            }";
                }
            }
            """";

#pragma warning disable format
        string user = """"
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            namespace Tests.OpenClass
            {
                [global::System.CodeDom.Compiler.GeneratedCodeAttribute("AvroNet", "1.0.0.0")]
                public partial class User : global::{|#0:Avro|}.Specific.ISpecificRecord
                {
                    public static readonly global::{|#1:Avro|}.Schema _SCHEMA = global::{|#2:Avro|}.Schema.Parse(SchemaJson);
                    public global::{|#3:Avro|}.Schema Schema { get => User._SCHEMA; }
                    public string Name { get; set; }
                    public int Age { get; set; } = 18;
                    public string Description { get; set; }
                    
                    public object Get(int fieldPos)
                    {
                        switch (fieldPos)
                        {
                            case 0: return this.Name;
                            case 1: return this.Age;
                            case 2: return this.Description;
                            default: throw new global::{|#4:Avro|}.AvroRuntimeException($"Bad index {fieldPos} in Get()");
                        }
                    }

                    public void Put(int fieldPos, object fieldValue)
                    {
                        switch (fieldPos)
                        {
                            case 0: this.Name = (string)fieldValue; break;
                            case 1: this.Age = (int)fieldValue; break;
                            case 2: this.Description = (string)fieldValue; break;
                            default: throw new global::{|#5:Avro|}.AvroRuntimeException($"Bad index {fieldPos} in Put()");
                        }

                    }

                }
                
            }
            
            """";
#pragma warning restore format

        DiagnosticResult[] diagnostics = [
            ..Enumerable.Range(0, 6).Select(i => DiagnosticResult.CompilerError("CS0400").WithLocation(i))
        ];

        await VerifyCS.VerifyGeneratorAsync(code, diagnostics,
            ("AvroModelAttribute.g.cs", Attribute),
            ("User.AvroModel.g.cs", user));
    }
#endif

    [Fact]
    public async Task Click()
    {
        const string code = """"
            using System;
            using AvroNet;
            
            namespace Tests.OpenClass;

            [AvroModel]
            public partial record Test
            {
                public const string SchemaJson = """
                {
                    "type": "record",
                    "name": "Test",
                    "namespace": "com.example.avro",
                    "fields": [
                        { "name": "null_field", "type": "null"},
                        { "name": "boolean_field", "type": "boolean"},
                        { "name": "int_field", "type": "int"},
                        { "name": "long_field", "type": "long"},
                        { "name": "float_field", "type": "float"},
                        { "name": "double_field", "type": "double"},
                        { "name": "string_field", "type": "string"},
                        { "name": "bytes_field", "type": "bytes"},
                        { "name": "array_field", "type": { "type": "array", "items": "int"} },
                        { "name": "array_field_null", "type": { "type": "array", "items": ["null", "int"]} },
                        { "name": "map_field", "type": { "type": "map", "values": "string"} },
                        { "name": "map_field_null", "type": { "type": "map", "values": ["null", "string"]} },
                        { "name": "enum_field", "type": { "type": "enum", "name": "ExampleEnum", "symbols": ["SYMBOL1", "SYMBOL2"]} },
                        { "name": "fixed_field", "type": { "type": "fixed", "name": "ExampleFixed", "size": 16} },
                        { "name": "union_field", "type": ["string", "int", { "type": "record", "name": "UnionRecord", "fields": [{ "name": "sub_field", "type": "string"}]}]},
                        { "name": "record_field", "type": { "type": "record", "name": "ExampleRecord", "fields": [{ "name": "sub_field", "type": "string"}]} },
                        { "name": "decimal_field_bytes", "type": { "type": "bytes", "logicalType": "decimal", "precision": 2 } },
                        { "name": "uuid", "type": { "type": "string", "logicalType": "uuid" } },
                        { "name": "date", "type": { "type": "int", "logicalType": "date" } },
                        { "name": "time_ms", "type": { "type": "int", "logicalType": "time-millis" } },
                        { "name": "time_us", "type": { "type": "long", "logicalType": "time-micros" } },
                        { "name": "timestamp_ms", "type": { "type": "long", "logicalType": "timestamp-millis" } },
                        { "name": "timestamp_us", "type": { "type": "long", "logicalType": "timestamp-micros" } },
                        { "name": "local_timestamp_ms", "type": { "type": "long", "logicalType": "local-timestamp-millis" } },
                        { "name": "local_timestamp_us", "type": { "type": "long", "logicalType": "local-timestamp-micros" } },
                        { "name": "null_boolean_field", "type": ["null", "boolean"]},
                        { "name": "null_int_field", "type": ["null", "int"]},
                        { "name": "null_long_field", "type": ["null", "long"]},
                        { "name": "null_float_field", "type": ["null", "float"]},
                        { "name": "null_double_field", "type": ["null", "double"]},
                        { "name": "null_string_field", "type": ["null", "string"]},
                        { "name": "null_bytes_field", "type": ["null", "bytes"]},
                        { "name": "null_array_field", "type": ["null", { "type": "array", "items": "int"} ]},
                        { "name": "null_array_field_null", "type": ["null", { "type": "array", "items": ["null", "int"]} ]},
                        { "name": "null_map_field", "type": ["null", { "type": "map", "values": "string"} ]},
                        { "name": "null_map_field_null", "type": ["null", { "type": "map", "values": ["null", "string"]} ]},
                        { "name": "null_enum_field", "type": ["null", "ExampleEnum" ]},
                        { "name": "null_fixed_field", "type": ["null", "ExampleFixed" ]},
                        { "name": "null_union_field", "type": ["null", "string", "int", "UnionRecord" ]},
                        { "name": "null_record_field", "type": ["null", "ExampleRecord" ]},
                        { "name": "null_decimal_field_bytes", "type": ["null", { "type": "bytes", "logicalType": "decimal", "precision": 2 }]},
                        { "name": "null_uuid", "type": ["null", { "type": "string", "logicalType": "uuid" }]},
                        { "name": "null_date", "type": ["null", { "type": "int", "logicalType": "date" }]},
                        { "name": "null_time_ms", "type": ["null", { "type": "int", "logicalType": "time-millis" }]},
                        { "name": "null_time_us", "type": ["null", { "type": "long", "logicalType": "time-micros" }]},
                        { "name": "null_timestamp_ms", "type": ["null", { "type": "long", "logicalType": "timestamp-millis" }]},
                        { "name": "null_timestamp_us", "type": ["null", { "type": "long", "logicalType": "timestamp-micros" }]},
                        { "name": "null_local_timestamp_ms", "type": ["null", { "type": "long", "logicalType": "local-timestamp-millis" }]},
                        { "name": "null_local_timestamp_us", "type": ["null", { "type": "long", "logicalType": "local-timestamp-micros" }]}
                    ]
                }
            """;
            }
            """";

        var diagnostic = DiagnosticResult.CompilerError("CS0261").WithArguments("Tests.OpenClass.User");

        await VerifyCS.VerifyGeneratorAsync(code,
            ("AvroModelAttribute.g.cs", Attribute),
            ("Test.AvroModel.g.cs", ""));
    }
}