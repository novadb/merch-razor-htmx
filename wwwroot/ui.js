// What htmx has no attribute for: the keys of the typeahead's combobox, the focus after "Show more", and showing a
// range from its start when a filter left less of it. Everything else is `hx-*` in the HTML (see _Range.cshtml, _SearchBox.cshtml).

// ---- Typeahead ------------------------------------------------------------------------------------
const searchForm = (event) => event.target.closest?.("form.search");

// Arrow keys move the highlight through the options (round), Escape closes the list. Enter opens the highlighted
// product or page; on "search all" (no address) or with nothing highlighted Enter submits the search as usual.
document.addEventListener("keydown", (event) => {
  const form = searchForm(event);
  if (!form) return;
  const list = form.querySelector(".suggest"), input = form.querySelector("input"), options = [...list.querySelectorAll("[role=option]")];
  const active = options.findIndex((option) => option.ariaSelected === "true");
  if (list.hidden || !options.length) return;
  if (event.key === "Escape") { list.hidden = true; input.ariaExpanded = false; }
  else if (event.key === "Enter" && options[active]?.dataset.href) location.href = options[active].dataset.href;
  else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
    const next = options.at((active + (event.key === "ArrowDown" ? 1 : -1)) % options.length);
    options.forEach((option) => option.ariaSelected = option === next);
    input.setAttribute("aria-activedescendant", next.id);
    next.scrollIntoView({ block: "nearest" });
  }
  else return;
  event.preventDefault();
});

// A click on an option: the product or page, or "search all", which submits the form. On mousedown, before the field loses focus.
document.addEventListener("mousedown", (event) => {
  const form = searchForm(event), option = event.target.closest?.("[role=option]");
  if (!form || !option) return;
  event.preventDefault();
  if (option.dataset.href) location.href = option.dataset.href; else form.requestSubmit();
});

document.addEventListener("focusout", (event) => {
  const form = searchForm(event);
  if (form) form.querySelector(".suggest").hidden = true;
});

// ---- Range ------------------------------------------------------------------------------------------
// While htmx updates the range or adds cards to it, it is `aria-busy`. A filter's answer replaces the range, which does not
// carry the attribute; "Show more" only adds cards, so the attribute is cleared here once they are in.
let scrolled = 0;
let cardsBefore = null; // the number of cards while a keyboard click on "Show more" runs

document.addEventListener("htmx:before:request", (event) => {
  const range = event.target.closest?.("#range");
  if (!range) return;
  range.ariaBusy = true;
  const more = event.target.closest(".range__more");
  cardsBefore = more && document.activeElement === more ? range.querySelectorAll(".grid .product-card").length : null;
});

document.addEventListener("htmx:before:swap", () => { scrolled = scrollY; });

document.addEventListener("htmx:after:swap", () => {
  const range = document.getElementById("range");
  if (!range) return;
  range.removeAttribute("aria-busy");
  // After a keyboard click the button is gone: the focus goes on to the first new card.
  if (cardsBefore !== null) range.querySelectorAll(".grid .product-card a")[cardsBefore]?.focus();
  cardsBefore = null;
  // The scroll position stays. Only when it cannot (the page got shorter than it) or shows no result any more (the
  // range ends above the window) the range is shown from its start, below the sticky header (its scroll-margin-top).
  const gone = range.lastElementChild.getBoundingClientRect().bottom < parseFloat(getComputedStyle(range).scrollMarginTop);
  if (Math.abs(scrollY - scrolled) > 1 || gone) range.scrollIntoView({ behavior: "instant" });
});
