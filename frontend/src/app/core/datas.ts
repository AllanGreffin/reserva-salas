/**
 * Conversões entre o <input type="datetime-local"> (hora local do navegador, sem fuso) e a API
 * (ISO 8601 em UTC). O fuso só existe na tela; o que trafega é sempre inequívoco (RN06).
 */

/** "2030-01-15T10:00" (hora local) → "2030-01-15T13:00:00.000Z" em UTC-3. */
export function localParaUtc(valorDoCampo: string): string {
  return new Date(valorDoCampo).toISOString();
}

/** "2030-01-15" (dia no fuso do navegador) → início desse dia, em UTC. */
export function inicioDoDiaUtc(dia: string): string {
  return new Date(`${dia}T00:00`).toISOString();
}

/**
 * "2030-01-15" → início do dia SEGUINTE, em UTC. Usado como limite exclusivo de um período, para que o
 * dia informado entre inteiro: "até 15/01" = [..., 16/01 00:00).
 */
export function fimDoDiaUtc(dia: string): string {
  const data = new Date(`${dia}T00:00`);
  data.setDate(data.getDate() + 1);
  return data.toISOString();
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
