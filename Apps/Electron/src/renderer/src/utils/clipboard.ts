import { clipboardApi } from '../api/clipboard'

/** Copy one executable path per line; resolves to whether the write succeeded. */
export async function copyLines(lines: string[]): Promise<boolean> {
  const result = await clipboardApi.writeLines(lines)
  return result.ok === true
}
