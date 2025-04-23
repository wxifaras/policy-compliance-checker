namespace PolicyComplianceCheckerApi.Prompts;

public record CorePrompts
{
    public static string GetSystemPrompt(string engagementLetter)
    {
        var systemPrompt = $@"You are a compliance analyst. Your task is to examine an Engagement Letter 
                            against company policies to identify any potential violations. 
                            List ONLY the specific violations found, if any. 
                            If no violations are found, state 'No violations found.'

                            Here is the Engagement Letter: {engagementLetter}";

        return systemPrompt;
    }

    public static string GetUserPrompt(string policyChunk)
    {
        var userPrompt = $@"Please analyze the Engagement Letter against the following company policy: {policyChunk}. 
                            Return only the violations found and be specific about which part of the policy is violated by which part of the Engagement Letter.
                            Return the result in markdown format for readability, but DO NOT include any code blocks, backticks, or markdown syntax indicators like ```.
                            List each violation on a new line.";

        return userPrompt;
    }

    public static string GetEvalSystemPrompt(string violation, string llmResponseChunk)
    {
        var evaluationPrompt = $@"
                You are an AI assistant tasked with evaluating the correctness of generated content. The generated content represents potential violations found in a policy. 
                Your goal is to assess whether the generated content aligns with the ground truth content provided.

                The evaluation is based on a 'correctness metric,' which measures how accurately the generated content identified potential policy violations and compares it to the ground truth content.
                You will be provided with both the generated content and the ground truth content.

                Your task is to:
                1. Compare the generated content against the ground truth content.
                2. Assign a rating based on the following scale:
                   - **1**: The content is incorrect.
                   - **3**: The content is partially correct but may lack key context or nuance, making it potentially misleading or incomplete compared to the ground truth content.
                   - **5**: The content is correct and complete based on the ground truth content.

                Additionally, you must provide a detailed explanation for the rating you selected.

                **Important Notes**:
                - The rating must always be one of the following values: 1, 3, or 5.
                - Construct a JSON object containing the Rating and your Thoughts. Return this JSON object as the response.

                **Input Data**:
                - Ground truth content: {violation}
                - Generated content: {llmResponseChunk}";

        return evaluationPrompt;
    }

    public static string GetSummarizeThoughtSystemPrompt(string combinedThoughts)
    {
        var summarizationPrompt = $@"
            You are an AI assistant tasked with summarizing feedback from multiple evaluations.
            Below is a list of detailed thoughts from various evaluations. Your task is to:

            1. Provide a concise summary of the feedback in a few sentences.

            **Detailed Thoughts**:
            {combinedThoughts}

            **Summary**:";

        return summarizationPrompt;
    }
}