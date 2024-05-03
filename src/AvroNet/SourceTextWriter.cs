﻿using AvroNet.Schemas;
using Microsoft.CodeAnalysis.CSharp;
using System.CodeDom.Compiler;
using System.Text.Json;

namespace AvroNet;

internal sealed class SourceTextWriter : IDisposable
{
    private readonly StringWriter _stream;
    private readonly IndentedTextWriter _writer;
    private readonly AvroModelOptions _options;
    private readonly Dictionary<ReadOnlyMemory<byte>, AvroSchema> _schemas;

    public SourceTextWriter(AvroModelOptions options)
    {
        _stream = new StringWriter();
        _writer = new IndentedTextWriter(_stream, AvroGenerator.TabString);
        _options = options;
        _schemas = new(new ReadOnlyMemoryComparer());
    }

    public void Write(AvroSchema schema)
    {
        _writer.WriteLine(AvroGenerator.AutoGeneratedComment);
        if (_options.UseNullableReferenceTypes)
            _writer.WriteLine("#nullable enable");
        if (_options.UseFileScopedNamespaces)
        {
            _writer.Write("namespace ");
            _writer.Write(_options.Namespace);
            _writer.WriteLine(";");
            _writer.WriteLine();
            WriteSchema(schema);
        }
        else
        {
            _writer.Write("namespace ");
            _writer.WriteLine(_options.Namespace);
            _writer.WriteLine("{");
            ++_writer.Indent;
            WriteSchema(schema);
            --_writer.Indent;
            _writer.WriteLine("}");
        }
        if (_options.UseNullableReferenceTypes)
            _writer.WriteLine("#nullable restore");
    }

    private void WriteSchema(AvroSchema schema)
    {
        var rawValue = schema.Name.GetRawValue();
        if (_schemas.ContainsKey(rawValue))
            return;

        switch (schema.GetTypeTag(_schemas))
        {
            case SchemaTypeTag.Null:
            case SchemaTypeTag.Boolean:
            case SchemaTypeTag.Int:
            case SchemaTypeTag.Long:
            case SchemaTypeTag.Float:
            case SchemaTypeTag.Double:
            case SchemaTypeTag.Bytes:
            case SchemaTypeTag.String:
            case SchemaTypeTag.Logical:
                return;

            case SchemaTypeTag.Array:
                WriteSchema(schema.AsArraySchema().ItemsSchema);
                return;

            case SchemaTypeTag.Map:
                WriteSchema(schema.AsMapSchema().ValuesSchema);
                return;

            case SchemaTypeTag.Union:
                var unionSchema = schema.AsUnionSchema();
                foreach (var member in unionSchema.Schemas)
                    WriteSchema(member);
                return;

            case SchemaTypeTag.Enumeration:
                _schemas[rawValue] = schema;
                WriteEnumSchema(schema.AsEnumSchema());
                return;

            case SchemaTypeTag.Fixed:
                _schemas[rawValue] = schema;
                WriteFixedSchema(schema.AsFixedSchema());
                return;

            case SchemaTypeTag.Record:
                _schemas[rawValue] = schema;
                var recordSchema = schema.AsRecordSchema();
                foreach (var field in recordSchema.Fields)
                    WriteSchema(field.Schema);
                WriteRecordSchema(recordSchema);
                return;

            case SchemaTypeTag.Error:
                _schemas[rawValue] = schema;
                var errorSchema = schema.AsErrorSchema();
                foreach (var field in schema.AsErrorSchema().Fields)
                    WriteSchema(field.Schema);
                WriteErrorSchema(errorSchema);
                return;

            default:
                throw new InvalidOperationException($"Unable to add name for schema '{schema.Name}' of type '{schema.GetTypeTag(_schemas)}'");
        }
    }

    private void WriteComment(JsonElement? documentation)
    {
        var comment = documentation?.GetString();
        if (!string.IsNullOrWhiteSpace(comment))
            _writer.WriteLine(SyntaxFactory.Comment(comment!).ToFullString());
    }

    private void WriteDefinitionStart(string typeDefinition, string typeIdentifier, string? baseTypeIdentifier = null)
    {
        _writer.Write('[');
        _writer.Write(AvroGenerator.GeneratedCodeAttribute);
        _writer.WriteLine(']');
        _writer.Write(_options.AccessModifier);
        _writer.Write(' ');
        _writer.Write(typeDefinition);
        _writer.Write(' ');
        _writer.Write(typeIdentifier);
        if (baseTypeIdentifier is not null)
        {
            _writer.Write(' ');
            _writer.Write(':');
            _writer.Write(' ');
            _writer.Write(baseTypeIdentifier);
        }
        _writer.WriteLine();
        _writer.WriteLine('{');
        ++_writer.Indent;
    }

    private void WriteDefinitionEnd()
    {
        --_writer.Indent;
        _writer.WriteLine('}');
    }

    private void WriteSchemaProperty(JsonElement json, string typeName, bool isOverride)
    {
        _writer.Write("public static readonly ");
        _writer.Write(AvroGenerator.AvroSchemaTypeName);
        _writer.Write(" _SCHEMA = ");
        _writer.Write(AvroGenerator.AvroSchemaTypeName);
        _writer.Write(".Parse(");
        var schemaJson = typeName == _options.Name
            ? AvroGenerator.AvroClassSchemaConstName
            : SymbolDisplay.FormatLiteral(json.GetRawText(), quote: true);
        _writer.Write(schemaJson);
        _writer.WriteLine(");");

        _writer.Write("public ");
        if (isOverride)
            _writer.Write("override ");
        _writer.Write(AvroGenerator.AvroSchemaTypeName);
        _writer.Write(" Schema { get => ");
        _writer.Write(typeName);
        _writer.Write('.');
        _writer.Write("_SCHEMA");
        _writer.WriteLine("; }");
    }

    private void WriteField(FieldSchema schema, string fieldName, TypeSymbol fieldType)
    {
        var defaultValue = fieldType.GetValue(schema.Default);
        WriteComment(schema.Documentation);

        _writer.Write("public ");
        if (!fieldType.IsNullable)
        {
            if (_options.UseRequiredProperties)
                _writer.Write("required ");
            else if (_options.UseNullableReferenceTypes)
                defaultValue ??= "default!";
        }
        _writer.Write(fieldType);
        _writer.Write(' ');
        _writer.Write(fieldName);
        if (_options.UseInitOnlyProperties)
            _writer.Write(" { get; init; }");
        else
            _writer.Write(" { get; set; }");
        if (defaultValue is not null)
        {
            _writer.Write(" = ");
            _writer.Write(defaultValue);
            _writer.Write(';');
        }
        _writer.WriteLine();
    }

    private void WriteRecordSchema(RecordSchema schema)
    {
        var name = Identifier.GetValid(schema.Name);

        WriteComment(schema.Documentation);
        WriteDefinitionStart(_options.DeclarationType, name, AvroGenerator.AvroISpecificRecordTypeName);
        WriteSchemaProperty(schema.Json, name, isOverride: false);

        var getPutBuilder = new GetPutBuilder(name, _writer.Indent, isOverride: false, _options);
        var fieldPosition = 0;
        foreach (var field in schema.Fields)
        {
            var fieldName = Identifier.GetValid(field.Name);
            var fieldType = TypeSymbol.FromSchema(field.Schema, nullable: false, _schemas, _options);
            WriteField(field, fieldName, fieldType);

            getPutBuilder.AddCase(fieldPosition++, fieldName, fieldType);
        }
        getPutBuilder.AddDefault();
        _writer.WriteLine();
        getPutBuilder.WriteTo(_writer);

        WriteDefinitionEnd();
        _writer.WriteLine();
    }

    private void WriteErrorSchema(ErrorSchema schema)
    {
        var name = Identifier.GetValid(schema.Name);

        WriteComment(schema.Documentation);
        WriteDefinitionStart(_options.DeclarationType, name, AvroGenerator.AvroISpecificRecordTypeName);
        WriteSchemaProperty(schema.Json, name, isOverride: false);

        var getPutBuilder = new GetPutBuilder(name, _writer.Indent, isOverride: false, _options);
        var fieldPosition = 0;
        foreach (var field in schema.Fields)
        {
            var fieldName = Identifier.GetValid(field.Name);
            var fieldType = TypeSymbol.FromSchema(field.Schema, nullable: false, _schemas, _options);
            WriteField(field, fieldName, fieldType);

            getPutBuilder.AddCase(fieldPosition++, fieldName, fieldType);
        }
        getPutBuilder.AddDefault();
        _writer.WriteLine();
        getPutBuilder.WriteTo(_writer);

        WriteDefinitionEnd();
        _writer.WriteLine();
    }

    private void WriteEnumSchema(EnumSchema schema)
    {
        WriteComment(schema.Documentation);
        var name = Identifier.GetValid(schema.Name);
        WriteDefinitionStart("enum", name);
        foreach (var field in schema.Symbols)
        {
            _writer.Write(Identifier.GetValid(field));
            _writer.WriteLine(',');
        }
        WriteDefinitionEnd();
        _writer.WriteLine();
    }

    private void WriteFixedSchema(FixedSchema schema)
    {
        WriteComment(schema.Documentation);
        var name = Identifier.GetValid(schema.Name);
        WriteDefinitionStart("partial class", name, AvroGenerator.AvroSpecificFixedTypeName);
        WriteSchemaProperty(schema.Json, name, isOverride: true);
        _writer.Write("public uint FixedSize { get => ");
        _writer.Write(schema.Size);
        _writer.WriteLine("; }");
        _writer.WriteLine();
        _writer.Write("public ");
        _writer.Write(name);
        _writer.Write("() : base(");
        _writer.Write(schema.Size);
        _writer.WriteLine(")");
        _writer.WriteLine("{");
        ++_writer.Indent;
        _writer.Write("((");
        _writer.Write(AvroGenerator.AvroGenericFixedTypeName);
        _writer.Write(')');
        _writer.Write("this");
        _writer.Write(')');
        _writer.Write(".Schema = (");
        _writer.Write(AvroGenerator.AvroFixedSchemaTypeName);
        _writer.Write(")");
        _writer.Write(name);
        _writer.WriteLine("._SCHEMA;");
        --_writer.Indent;
        _writer.WriteLine("}");
        WriteDefinitionEnd();
        _writer.WriteLine();
    }

    public override string ToString() => _stream.ToString();

    public void Dispose()
    {
        _stream.Dispose();
        _writer.Dispose();
    }
}
