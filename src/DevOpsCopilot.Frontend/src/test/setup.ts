import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

declare global {
  // Tells React that act(...) is supported here; set globally because the tests drive the clock
  // with React's own act rather than only through Testing Library's wrappers.
  var IS_REACT_ACT_ENVIRONMENT: boolean;
}

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

// Vitest runs without globals, so Testing Library's automatic cleanup is registered here.
afterEach(() => {
  cleanup();
});
