using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Google.Protobuf.Collections;
using Microsoft.KernelMemory;

namespace SharpNlp.Core;


public static class Utils
{

    private static readonly Regex _regex = new Regex("<.+>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ReadResourceAsText(string fileName, string folder)
    {
        var @namespace = typeof(PromptProvider).Namespace!;
        var assembly = Assembly.GetExecutingAssembly();

        // Resources are mapped like types, using the namespace and appending "." (dot) and the file name
        var resourceName = $"{@namespace}.{folder}." + fileName;
        using var resource = assembly.GetManifestResourceStream(resourceName);

        if (resource == null)
            throw new ConfigurationException($"{resourceName} resource not found");

        // Return the resource content, in text format.
        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }

    public static DocumentInfo MergeDocuments(List<DocumentInfo> documents)
    {
        var entities = new Dictionary<string, HashSet<string>>
        {
            { DocumentInfo.LABEL_PERSON, new() },
            { DocumentInfo.LABEL_ORGANISATION, new() },
            { DocumentInfo.LABEL_DATE, new() },
            { DocumentInfo.LABEL_MINERALDEPOSIT, new() },
            { DocumentInfo.LABEL_RESERVOIR, new() },
            { DocumentInfo.LABEL_NPT, new() }
        };

        foreach (var document in documents)
        {
            MergeEntities(entities[DocumentInfo.LABEL_PERSON], document.PERSON);
            MergeEntities(entities[DocumentInfo.LABEL_ORGANISATION], document.ORGANISATION);
            MergeEntities(entities[DocumentInfo.LABEL_DATE], document.DATE);
            MergeEntities(entities[DocumentInfo.LABEL_MINERALDEPOSIT], document.MINERALDEPOSIT);
            MergeEntities(entities[DocumentInfo.LABEL_RESERVOIR], document.RESERVOIR);
            MergeEntities(entities[DocumentInfo.LABEL_NPT], document.NPT);
        }

        var result = new DocumentInfo
        {
            PERSON = entities[DocumentInfo.LABEL_PERSON].ToList(),
            ORGANISATION = ProcessOrganisations(entities[DocumentInfo.LABEL_ORGANISATION].ToList()),
            DATE = ProcessDates(entities[DocumentInfo.LABEL_DATE].ToList()),
            MINERALDEPOSIT = entities[DocumentInfo.LABEL_MINERALDEPOSIT].ToList(),
            RESERVOIR = entities[DocumentInfo.LABEL_RESERVOIR].ToList(),
            NPT = entities[DocumentInfo.LABEL_NPT].ToList()
        };

        return result;
    }

    private static void MergeEntities(HashSet<string> mergedEntities, List<string>? documentEntities)
    {
        if (documentEntities is null || !documentEntities.Any()) return;

        foreach (var docEntity in documentEntities)
        {
            mergedEntities.Add(docEntity);
        }
    }

    private static List<string> ProcessOrganisations(List<string> entities)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            entities[i] = entities[i].Replace('<', '"').Replace('>', '"');
        }

        return entities;
    }

    private static List<string> ProcessDates(List<string> entities)
    {
        List<string> result = new();

        for (int i = 0; i < entities.Count; i++)
        {
            if (DateOnly.TryParseExact(entities[i], "dd.MM.yyyy", out var date))
            {
                result.Add(entities[i]);
            }
        }

        return result;
    }

    //private static List<string> ProcessEntities(List<string> entities)
    //{
    //    for (int i = 0; i < entities.Count; i++)
    //    {
    //        var match = _regex.Match(entities[i]);

    //        while (match.Success)
    //        {
    //            var newEntity = new StringBuilder(entities[i]);

    //            newEntity.Replace('<', '"', match.Index, 1);
    //            newEntity.Replace('>', '"', match.Index + match.Length - 1, 1);

    //            entities[i] = newEntity.ToString();
    //            match = match.NextMatch();
    //        }
    //    }

    //    return entities;
    //}
}
