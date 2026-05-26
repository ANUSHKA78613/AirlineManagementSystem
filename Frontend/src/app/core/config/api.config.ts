export const apiConfig = {
  gatewayBaseUrl: 'http://localhost:9118',
  defaultFrontendUrl: 'http://localhost:4200',
} as const;

export function gatewayUrl(path: string): string {
  return `${apiConfig.gatewayBaseUrl}${path}`;
}

export function frontendUrl(path = ''): string {
  const origin = typeof window === 'undefined' ? apiConfig.defaultFrontendUrl : window.location.origin;
  return `${origin}${path}`;
}
