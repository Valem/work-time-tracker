import { render } from 'preact';
import { App } from './app';
import { localeNamed } from './locale/registry';
import './styles.css';

const locale = localeNamed(new URLSearchParams(window.location.search).get('locale'));
document.documentElement.lang = locale.language.tag;
document.title = locale.language.productName;

const root = document.getElementById('app');
if (root !== null) {
  render(<App locale={locale} />, root);
}
