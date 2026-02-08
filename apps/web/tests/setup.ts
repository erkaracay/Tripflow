// Provide localStorage for tests that import auth (which reads storage at load time)
const storage = new Map<string, string>()
globalThis.localStorage = {
  getItem: (key: string) => storage.get(key) ?? null,
  setItem: (key: string, value: string) => {
    storage.set(key, value)
  },
  removeItem: (key: string) => {
    storage.delete(key)
  },
  clear: () => storage.clear(),
  get length() {
    return storage.size
  },
  key: (i: number) => Array.from(storage.keys())[i] ?? null,
}
