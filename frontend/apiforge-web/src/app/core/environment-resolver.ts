export type VariableMap = Record<string, string | undefined>;

export interface ResolutionResult {
  value: string;
  missing: string[];
}

export function resolveTemplateVariables(input: string, variables: VariableMap): ResolutionResult {
  const missing = new Set<string>();
  const value = input.replace(/\{\{\s*([^{}\s]+)\s*\}\}/g, (_match, key: string) => {
    const replacement = variables[key];
    if (replacement === undefined || replacement === '') {
      missing.add(key);
      return `{{${key}}}`;
    }

    return replacement;
  });

  return { value, missing: [...missing] };
}
