﻿using AvroNet.Schemas;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;

namespace AvroNet.Output;

internal readonly struct ApacheAvroSourceTextWriter(IndentedStringBuilder builder, SourceTextWriterContext context)
{
    public static string WriteFromContext(SourceTextWriterContext context)
    {
        var builder = new IndentedStringBuilder();
        var writer = new ApacheAvroSourceTextWriter(builder, context);

        using var document = JsonDocument.Parse(context.SchemaJson);
        writer.Write(new AvroSchema(document.RootElement));

        return builder.ToString();
    }

    public override string ToString() => builder.ToString();

    private void Write(AvroSchema schema)
    {
        builder.AppendLine(AvroGenerator.AutoGeneratedComment);
        if (context.UseNullableReferenceTypes)
            builder.AppendLine("#nullable enable");
        if (context.UseFileScopedNamespaces)
        {
            builder.AppendLine($"namespace {context.Namespace};");
            builder.AppendLine();
            WriteSchema(schema);
        }
        else
        {
            builder.AppendLine($"namespace {context.Namespace}");
            using (BlockStatement())
            {
                WriteSchema(schema);
            }
        }
        if (context.UseNullableReferenceTypes)
            builder.AppendLine("#nullable restore");
    }

    private void WriteSchema(AvroSchema schema)
    {
        if (!context.Schemas.TryRegister(schema))
            return;

        switch (context.Schemas.GetTypeTag(schema))
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
                WriteEnumSchema(schema.AsEnumSchema());
                return;

            case SchemaTypeTag.Fixed:
                WriteFixedSchema(schema.AsFixedSchema());
                return;

            case SchemaTypeTag.Record:
                var recordSchema = schema.AsRecordSchema();
                foreach (var field in recordSchema.Fields)
                    WriteSchema(field.Schema);
                WriteRecordSchema(recordSchema);
                return;

            case SchemaTypeTag.Error:
                var errorSchema = schema.AsErrorSchema();
                foreach (var field in schema.AsErrorSchema().Fields)
                    WriteSchema(field.Schema);
                WriteErrorSchema(errorSchema);
                return;

            default:
                throw new InvalidOperationException($"Unable to add name for schema '{schema.Name}' of type '{context.Schemas.GetTypeTag(schema)}'");
        }
    }

    private void WriteComment(JsonElement? documentation)
    {
        var comment = documentation?.GetString();
        if (!string.IsNullOrWhiteSpace(comment))
        {
            builder.AppendLine("/// <summary>");
            builder.Append("/// ");
            var span = comment.AsSpan();
            for (int i = 0; i < span.Length; ++i)
            {
                switch (span[i])
                {
                    case '\r':
                        break;
                    case '\n':
                        builder.AppendLine();
                        builder.Append("/// ");
                        break;
                    default:
                        builder.Append(span[i]);
                        break;
                }
            }
            builder.AppendLine();
            builder.AppendLine("/// </summary>");
        }
    }

    private void WriteTypeDeclaration(string typeDefinition, string typeIdentifier, string? baseTypeIdentifier = null)
    {
        builder.AppendLine($"[{AvroGenerator.GeneratedCodeAttribute}]");
        if (baseTypeIdentifier is not null)
            builder.AppendLine($"{context.AccessModifier} {typeDefinition} {typeIdentifier} : {baseTypeIdentifier}");
        else
            builder.AppendLine($"{context.AccessModifier} {typeDefinition} {typeIdentifier}");
    }

    private BlockStatement BlockStatement() => new(builder);

    private void WriteSchemaProperty(JsonElement json, string typeName, bool isOverride)
    {
        var schemaJson = typeName == context.Name ? AvroGenerator.AvroClassSchemaConstName : SymbolDisplay.FormatLiteral(json.GetRawText(), quote: true);
        builder.AppendLine($"public static readonly {AvroGenerator.AvroSchemaTypeName} _SCHEMA = {AvroGenerator.AvroSchemaTypeName}.Parse({schemaJson});");
        builder.AppendLine($$"""public {{(isOverride ? "override " : "")}}{{AvroGenerator.AvroSchemaTypeName}} Schema { get => {{typeName}}._SCHEMA; }""");
    }

    private void WriteField(FieldInfo field)
    {
        WriteComment(field.Schema.Documentation);

        var modifiers = !field.Type.IsNullable && context.UseRequiredProperties ? "public required" : "public";
        var set = context.UseInitOnlyProperties ? "init" : "set";
        var defaultValue = field.Type.GetValue(field.Schema.Default);
        if (!field.Type.IsNullable && context.UseNullableReferenceTypes)
            defaultValue ??= "default!";

        if (defaultValue is not null)
            builder.AppendLine($$"""{{modifiers}} {{field.Type}} {{field.Name}} { get; {{set}}; } = {{defaultValue}};""");
        else
            builder.AppendLine($$"""{{modifiers}} {{field.Type}} {{field.Name}} { get; {{set}}; }""");
    }

    private readonly record struct FieldInfo(FieldSchema Schema, string Name, TypeSymbol Type, int Position);

    private List<FieldInfo> GetFieldInfo(IEnumerable<FieldSchema> schemas, int length)
    {
        var fields = new List<FieldInfo>(length);
        var index = 0;
        foreach (var schema in schemas)
        {
            fields.Add(new FieldInfo(
                schema,
                Identifier.GetValid(schema.Name),
                TypeSymbol.FromSchema(schema.Schema, nullable: false, context),
                index++));
        }
        return fields;
    }

    private void WriteGetMethod(List<FieldInfo> fields, bool isOverride)
    {
        var objectType = context.UseNullableReferenceTypes ? "object?" : "object";
        builder.AppendLine($"public {(isOverride ? "override " : "")}{objectType} Get(int fieldPos)");
        using (BlockStatement())
        {
            builder.AppendLine("switch (fieldPos)");
            using (BlockStatement())
            {
                foreach (var field in fields)
                    builder.AppendLine($"case {field.Position}: return this.{field.Name};");
                builder.AppendLine("""default: throw new global::Avro.AvroRuntimeException($"Bad index {fieldPos} in Get()");""");
            }
        }
    }

    private void WritePutMethod(string ownerTypeName, List<FieldInfo> fields, bool isOverride)
    {
        var objectType = context.UseNullableReferenceTypes ? "object?" : "object";
        builder.AppendLine($"public {(isOverride ? "override " : "")}void Put(int fieldPos, {objectType} fieldValue)");
        using (BlockStatement())
        {
            builder.AppendLine("switch (fieldPos)");
            using (BlockStatement())
            {
                var parameterName = context.UseNullableReferenceTypes ? "fieldValue!" : "fieldValue";
                if (context.UseInitOnlyProperties)
                {
                    var setPrefix = context.UseUnsafeAccessors ? "" : $"{ownerTypeName}Reflection.";
                    foreach (var field in fields)
                        builder.AppendLine($"case {field.Position}: {setPrefix}Set_{field.Name}(this, ({field.Type}){parameterName}); break;");
                }
                else
                {
                    foreach (var field in fields)
                        builder.AppendLine($"case {field.Position}: this.{field.Name} = ({field.Type}){parameterName}; break;");
                }
                builder.AppendLine("""default: throw new global::Avro.AvroRuntimeException($"Bad index {fieldPos} in Put()");""");
            }

            if (context.UseInitOnlyProperties && context.UseUnsafeAccessors)
            {
                foreach (var field in fields)
                {
                    builder.AppendLine($"""[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_{field.Name}")]""");
                    builder.AppendLine($"extern static void Set_{field.Name}({ownerTypeName} obj, {field.Type} value);");
                }
            }
        }

        if (context.UseInitOnlyProperties && !context.UseUnsafeAccessors)
        {
            builder.AppendLine($"private static class {ownerTypeName}Reflection");
            using (BlockStatement())
            {
                foreach (var field in fields)
                    builder.AppendLine($"""public static readonly Action<{ownerTypeName}, {field.Type}> Set_{field.Name} = CreateSetter<{field.Type}>("{field.Name}");""");

                builder.AppendLine($"private static Action<{ownerTypeName}, TProperty> CreateSetter<TProperty>(string propertyName)");
                using (BlockStatement())
                {
                    builder.AppendLine($"""var objParam = global::System.Linq.Expressions.Expression.Parameter(typeof({ownerTypeName}), "obj");""");
                    builder.AppendLine($"""var valueParam = global::System.Linq.Expressions.Expression.Parameter(typeof(TProperty), "value");""");
                    if (context.UseNullableReferenceTypes)
                        builder.AppendLine($"""var property = global::System.Linq.Expressions.Expression.Property(objParam, typeof({ownerTypeName}).GetProperty(propertyName)!);""");
                    else
                        builder.AppendLine($"""var property = global::System.Linq.Expressions.Expression.Property(objParam, typeof({ownerTypeName}).GetProperty(propertyName));""");
                    builder.AppendLine($"""var assign = global::System.Linq.Expressions.Expression.Assign(property, valueParam);""");
                    builder.AppendLine($"""var lambda = global::System.Linq.Expressions.Expression.Lambda<Action<{ownerTypeName}, TProperty>>(assign, objParam, valueParam);""");
                    builder.AppendLine($"""return lambda.Compile();""");
                }
            }
        }
    }

    private void WriteRecordSchema(RecordSchema schema)
    {
        var name = Identifier.GetValid(schema.Name);

        WriteComment(schema.Documentation);
        WriteTypeDeclaration(context.DeclarationType, name, AvroGenerator.AvroISpecificRecordTypeName);
        using (BlockStatement())
        {
            WriteSchemaProperty(schema.Json, name, isOverride: false);
            var fields = GetFieldInfo(schema.Fields, schema.FieldsLength);
            foreach (var field in fields)
                WriteField(field);
            WriteGetMethod(fields, isOverride: false);
            WritePutMethod(name, fields, isOverride: false);
        }
        builder.AppendLine();
    }

    private void WriteErrorSchema(ErrorSchema schema)
    {
        var name = Identifier.GetValid(schema.Name);

        WriteComment(schema.Documentation);
        WriteTypeDeclaration(context.DeclarationType, name, AvroGenerator.AvroSpecificExceptionTypeName);
        using (BlockStatement())
        {
            WriteSchemaProperty(schema.Json, name, isOverride: true);
            var fields = GetFieldInfo(schema.Fields, schema.FieldsLength);
            foreach (var field in fields)
                WriteField(field);
            WriteGetMethod(fields, isOverride: true);
            WritePutMethod(name, fields, isOverride: true);
        }
        builder.AppendLine();
    }

    private void WriteEnumSchema(EnumSchema schema)
    {
        WriteComment(schema.Documentation);
        var name = Identifier.GetValid(schema.Name);
        WriteTypeDeclaration("enum", name);
        using (BlockStatement())
        {
            foreach (var field in schema.Symbols)
                builder.AppendLine($"{Identifier.GetValid(field)},");
        }
        builder.AppendLine();
    }

    private void WriteFixedSchema(FixedSchema schema)
    {
        WriteComment(schema.Documentation);
        var name = Identifier.GetValid(schema.Name);
        WriteTypeDeclaration("partial class", name, AvroGenerator.AvroSpecificFixedTypeName);
        using (BlockStatement())
        {
            WriteSchemaProperty(schema.Json, name, isOverride: true);
            builder.AppendLine($$"""public uint FixedSize { get => {{schema.Size}}; }""");
            builder.AppendLine($"public {name}() : base({schema.Size})");
            using (BlockStatement())
            {
                builder.AppendLine($"(({AvroGenerator.AvroGenericFixedTypeName})this).Schema = ({AvroGenerator.AvroFixedSchemaTypeName}){name}._SCHEMA;");
            }
        }
        builder.AppendLine();
    }
}

internal readonly ref struct BlockStatement
{
    private readonly IndentedStringBuilder _builder;
    public BlockStatement(IndentedStringBuilder builder)
    {
        _builder = builder;
        _builder.AppendLine("{");
        _builder.IncrementIndentation();
    }
    public void Dispose()
    {
        _builder.DecrementIndentation();
        _builder.AppendLine("}");
    }
}
