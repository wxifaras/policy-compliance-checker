﻿namespace PolicyComplianceCheckerApi.Prompts;

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
                            Return the result in markdown format for readability, but DO NOT include any code blocks, backticks, or markdown syntax indicators like ```.";

        return userPrompt;
    }
}