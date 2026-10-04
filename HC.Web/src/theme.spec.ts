/**
 * Guards the storefront theme.
 *
 * The look of the shop is set once, in src/scss/, and it is easy to switch off by accident: a
 * stray colour, a dropped @use in styles.scss, or the style entry moving back ahead of Bootstrap
 * in angular.json all change it without anybody touching a component. These specs read the values
 * the browser actually computed, so they fail the moment the theme stops reaching the page.
 *
 * (Karma loads src/styles.scss, which is where the tokens live, so these are real computed values
 * rather than a re-reading of the source.)
 */
describe('Storefront theme', () => {
  let probe: HTMLElement;

  beforeEach(() => {
    probe = document.createElement('div');
    probe.innerHTML = `
      <div class="header-promo">
        <p><span class="promo-item">30-Day Easy Returns</span></p>
      </div>
      <div class="container"></div>
      <h2>Shop by Category</h2>
      <button type="button" class="btn btn-primary">Add to cart</button>
      <span class="text-accent">Price</span>
      <input class="form-control" type="text">
    `;
    document.body.appendChild(probe);
  });

  afterEach(() => {
    probe.remove();
  });

  const token = (name: string) =>
    getComputedStyle(document.documentElement).getPropertyValue(name).trim();

  it('sets the brand typeface on the page and on headings', () => {
    expect(getComputedStyle(document.body).fontFamily).toContain('Manrope');
    expect(token('--font-family')).toContain('Manrope');
    expect(token('--font-heading')).toContain('Manrope');

    const heading = probe.querySelector('h2') as HTMLElement;
    expect(getComputedStyle(heading).fontFamily).toContain('Manrope');
  });

  it('uses the reference ink palette for text, buttons and links', () => {
    expect(token('--primary')).toBe('#121212');
    expect(token('--body-bg')).toBe('#ffffff');
    expect(token('--body-color')).toBe('#3d3d3d');
    expect(token('--text-muted')).toBe('#767676');

    const button = probe.querySelector('.btn-primary') as HTMLElement;
    expect(getComputedStyle(button).backgroundColor).toBe('rgb(18, 18, 18)');
    expect(getComputedStyle(button).color).toBe('rgb(255, 255, 255)');

    // Prices are marked up with .text-accent; on the reference they read as ink, not as a colour.
    const price = probe.querySelector('.text-accent') as HTMLElement;
    expect(getComputedStyle(price).color).toBe('rgb(18, 18, 18)');
  });

  it('shapes controls the way the reference does: 6px buttons, square imagery, square inputs', () => {
    expect(token('--radius-button')).toBe('6px');
    expect(token('--radius-media')).toBe('0px');
    expect(token('--radius-input')).toBe('2px');

    const button = probe.querySelector('.btn-primary') as HTMLElement;
    const buttonStyle = getComputedStyle(button);
    expect(buttonStyle.borderRadius).toBe('6px');
    expect(buttonStyle.textTransform).toBe('uppercase');
    expect(buttonStyle.fontWeight).toBe('700');

    const input = probe.querySelector('.form-control') as HTMLElement;
    expect(getComputedStyle(input).borderRadius).toBe('2px');
    expect(getComputedStyle(input).boxShadow).toBe('none');
  });

  it('keeps depth flat: cards and panels are not shadowed or lifted', () => {
    expect(token('--shadow-sm')).toContain('1px 2px');

    const card = document.createElement('div');
    card.className = 'product-card';
    card.innerHTML = '<div class="product-body"><div class="product-name"><a href="#">Piece</a></div></div>';
    probe.appendChild(card);
    expect(getComputedStyle(card).boxShadow).toBe('none');
  });

  it('carries the service strip above the header and the wide page', () => {
    const promo = probe.querySelector('.header-promo') as HTMLElement;
    const promoStyle = getComputedStyle(promo);
    expect(promoStyle.backgroundColor).toBe('rgb(18, 18, 18)');

    // The strip and the bar are two halves of one fixed header and the page is pushed down by
    // their total: 34 + 72 on a desktop, 30 + 58 once the phone media query takes over.
    const stripHeight = parseFloat(token('--promo-height'));
    const barHeight = parseFloat(token('--navbar-bar'));
    const onPhone = window.innerWidth <= 768;
    expect(stripHeight + barHeight).toBe(onPhone ? 88 : 106);
    expect(parseFloat(promoStyle.height)).toBe(stripHeight);
    expect(parseFloat(getComputedStyle(document.body).paddingTop)).toBe(stripHeight + barHeight);

    const item = probe.querySelector('.header-promo .promo-item') as HTMLElement;
    expect(getComputedStyle(item).textTransform).toBe('uppercase');

    const container = probe.querySelector('.container') as HTMLElement;
    expect(getComputedStyle(container).maxWidth).toBe('1400px');
  });
});
