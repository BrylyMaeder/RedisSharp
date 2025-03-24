using RedisSharp.Contracts;
using RediSearchClient.Indexes;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace RedisSharp.Index.Generation
{
    public class IndexDefinitionBuilder
    {
        public static (RediSearchIndexDefinition IndexDefinition, string IndexHash) Build(IAsyncModel document)
        {
            var analysis = ModelAnalyzer.Analyze(document);
            var builder = new RediSearchSchemaFieldBuilder();
            var schemaDetails = new StringBuilder();
            var schemaFields = new List<IRediSearchSchemaField>();

            foreach (var entry in analysis.IndexableEntries)
            {
                schemaDetails.Append(entry.MemberName).Append(":").Append(entry.IndexType).Append(";");

                IRediSearchSchemaField field;

                switch (entry.IndexType)
                {
                    case IndexType.Tag: field = builder.Tag(entry.MemberName); break;
                    case IndexType.Text: field = builder.Text(entry.MemberName); break;
                    case IndexType.Numeric: field = builder.Numeric(entry.MemberName); break;
                    default: continue;
                }

                schemaFields.Add(field);
            }

            if (schemaFields.Count == 0) return (null, null);

            var indexHash = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(schemaDetails.ToString())));
            var definition = RediSearchIndex.OnHash()
                .ForKeysWithPrefix($"{analysis.IndexName}:")
                .WithSchema(schemaFields.ToArray()).Build();

            return (definition, indexHash);
        }
    }
}
