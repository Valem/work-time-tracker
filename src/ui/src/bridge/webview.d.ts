/** What WebView2 gives the page to talk to its host: messages, nothing else. */
interface HostWebView {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: { readonly data: unknown }) => void): void;
}

interface Window {
  readonly chrome?: { readonly webview?: HostWebView };
}
