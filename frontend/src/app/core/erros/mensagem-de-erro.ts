import { HttpErrorResponse } from '@angular/common/http';

/** Corpo de erro da API (RFC 9457). */
interface ProblemDetails {
  title?: string;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
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
    case 409:
      return 'Esse horário já está reservado nesta sala. Escolha outro horário ou outra sala.';
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
