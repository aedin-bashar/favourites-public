import { FaviconUrlPipe } from './favicon-url.pipe';

describe('FaviconUrlPipe', () => {
  let pipe: FaviconUrlPipe;

  beforeEach(() => {
    pipe = new FaviconUrlPipe();
  });

  it('requests the favicon by hostname only, never the full URL', () => {
    const result = pipe.transform('https://example.com/secret/path?token=abc123');

    expect(result).toBe('https://www.google.com/s2/favicons?domain=example.com&sz=32');
    expect(result).not.toContain('secret');
    expect(result).not.toContain('token');
  });

  it('supports a custom size', () => {
    expect(pipe.transform('https://example.com/x', 16)).toBe(
      'https://www.google.com/s2/favicons?domain=example.com&sz=16'
    );
  });

  it('returns a blank data URI for unparsable URLs instead of leaking the raw value', () => {
    const result = pipe.transform('not a url');

    expect(result).toMatch(/^data:image\/gif;base64,/);
  });
});
