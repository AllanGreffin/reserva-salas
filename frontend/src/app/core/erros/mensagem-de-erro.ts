import { HttpErrorResponse } from '@angular/common/http';

/** Corpo de erro da API (RFC 9457). */
interface ProblemDetails {
  codigo?: string;
  title?: string;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export const CODIGO_VERSAO_DESATUALIZADA = 'VERSAO_DESATUALIZADA';

/** Verdadeiro se a edição foi recusada porque outra pessoa alterou a reserva antes (D1). */
export function ehVersaoDesatualizada(erro: unknown): boolean {
  return (
    erro instanceof HttpErrorResponse &&
    erro.status === 409 &&
    (erro.error as ProblemDetails | null)?.codigo === CODIGO_VERSAO_DESATUALIZADA
  );
}

/**
 * Converte um erro HTTP numa mensagem para o usuário. 403, 404 e 409 têm texto próprio, voltado à
 * ação na tela; 400 e 422 usam o texto da API, que explica a regra violada.
 */
export function mensagemDeErro(erro: unknown): string {
  if (!(erro instanceof HttpErrorResponse)) return 'Ocorreu um erro inesperado.';

  const problema = (erro.error ?? {}) as ProblemDetails;

  switch (erro.status) {
    case 0:
      return 'Não foi possível conectar ao servidor. Verifique sua conexão.';
    case 401:
      return problema.detail ?? 'Sua sessão expirou. Entre novamente.';
    case 403:
      return 'Você não tem permissão para alterar esta reserva. Só o responsável ou um administrador pode.';
    case 404:
      return 'A reserva não foi encontrada.';
    // Dois 409 com causas diferentes, distinguidos pelo código.
    case 409:
      return problema.codigo === CODIGO_VERSAO_DESATUALIZADA
        ? 'Esta reserva foi alterada por outra pessoa enquanto você editava. Os dados foram recarregados; revise e salve novamente.'
        : 'Esse horário já está reservado nesta sala. Escolha outro horário ou outra sala.';
    case 429:
      return 'Muitas tentativas seguidas. Aguarde um minuto e tente novamente.';
  }

  if (erro.status === 400 && problema.errors) {
    return Object.values(problema.errors).flat()[0] ?? 'Dados inválidos.';
  }

  if (erro.status >= 500) {
    const rastreio = problema.traceId ? ` (código para o suporte: ${problema.traceId})` : '';
    return `Ocorreu um erro inesperado no servidor${rastreio}.`;
  }

  return problema.detail ?? 'Não foi possível concluir a operação.';
}
