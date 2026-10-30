// Export the editable SVG sources; this utility has no application/runtime dependency.
const fs = require('node:fs');
const path = require('node:path');
const { Resvg } = require('@resvg/resvg-js');

for (const name of ['mainmenu', 'invoicelist', 'movementform']) {
  const base = path.join(__dirname, `wireframe-${name}`);
  const svg = fs.readFileSync(`${base}.svg`);
  const renderer = new Resvg(svg, { font: { loadSystemFonts: true, defaultFontFamily: 'Arial' } });
  const png = renderer.render();
  fs.writeFileSync(`${base}.png`, png.asPng());
  console.log(`${name}: ${png.width} x ${png.height}`);
}
