const sortKeys = (value: unknown): unknown => {
  if (Array.isArray(value)) {
    return value.map(sortKeys);
  }
  if (value !== null && typeof value === "object") {
    return Object.keys(value)
      .sort()
      .reduce<Record<string, unknown>>((acc, key) => {
        acc[key] = sortKeys((value as Record<string, unknown>)[key]);
        return acc;
      }, {});
  }
  return value;
};

const isJsonEqual = (a: string, b: string): boolean => {
  try {
    const objA = sortKeys(JSON.parse(a));
    const objB = sortKeys(JSON.parse(b));
    return JSON.stringify(objA) === JSON.stringify(objB);
  } catch {
    return false;
  }
};

export { isJsonEqual };
