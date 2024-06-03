using Microsoft.KernelMemory.Prompts;

namespace SharpNlp.Core;

public class PromptProvider : IPromptProvider
{
    public string ReadPrompt(string promptName)
    {
        return Utils.ReadResourceAsText($"{promptName}.txt", "Prompts");
    }
}
