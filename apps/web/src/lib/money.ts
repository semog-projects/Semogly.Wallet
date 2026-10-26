export const MAX_DEPOSIT = 1_000_000

/** Aceita "1.234,56", "1234,56" ou "1234.56"; null se não for um valor monetário válido. */
export function parseAmount(raw: string): number | null {
  const normalized = raw.includes(',') ? raw.replace(/\./g, '').replace(',', '.') : raw
  if (!/^\d+(\.\d{1,2})?$/.test(normalized.trim())) {
    return null
  }

  const value = Number(normalized)
  return value > 0 && value <= MAX_DEPOSIT ? value : null
}
