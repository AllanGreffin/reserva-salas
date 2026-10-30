/**
 * Conversões entre o <input type="datetime-local"> (hora local do navegador, sem fuso) e a API
 * (ISO 8601 em UTC). O fuso só existe na tela; o que trafega é sempre inequívoco (RN06).
 */

/** "2030-01-15T10:00" (hora local) → "2030-01-15T13:00:00.000Z" em UTC-3. */
export function localParaUtc(valorDoCampo: string): string {
  return new Date(valorDoCampo).toISOString();
}

/** ISO 8601 da API → "yyyy-MM-ddTHH:mm" no fuso do navegador, para preencher o campo. */
export function utcParaLocal(iso: string): string {
  const data = new Date(iso);
  const doisDigitos = (n: number) => String(n).padStart(2, '0');
  return (
    `${data.getFullYear()}-${doisDigitos(data.getMonth() + 1)}-${doisDigitos(data.getDate())}` +
    `T${doisDigitos(data.getHours())}:${doisDigitos(data.getMinutes())}`
  );
}
