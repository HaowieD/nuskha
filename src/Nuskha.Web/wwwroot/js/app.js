// Minimal glue for htmx: surface failed requests to the user instead of failing silently.
document.addEventListener('htmx:responseError', function (e) {
  var status = e.detail.xhr ? e.detail.xhr.status : 0;
  if (status === 401) return; // handled by HX-Redirect to the login page
  var toast = document.getElementById('toast');
  if (!toast) return;
  var key = status === 429 ? 'data-msg-429' : 'data-msg-error';
  toast.textContent = toast.getAttribute(key) || 'Error';
  toast.classList.remove('hidden');
  window.setTimeout(function () { toast.classList.add('hidden'); }, 4000);
});

// Confirmation for plain (non-htmx) forms: <button data-confirm="Are you sure?">. Kept in a file, not inline, for the CSP.
document.addEventListener('submit', function (e) {
  var button = e.submitter;
  var message = button && button.getAttribute('data-confirm');
  if (message && !window.confirm(message)) e.preventDefault();
});

// ---- Markup editor: toolbar, write/preview tabs, spoilers. Delegated, so it also works on htmx-swapped fragments. ----
(function () {
  var WRAP = { bold: ['**', '**'], italic: ['*', '*'], strike: ['~~', '~~'], code: ['`', '`'], spoiler: ['||', '||'] };

  function apply(textarea, action) {
    var start = textarea.selectionStart, end = textarea.selectionEnd, value = textarea.value;
    var selected = value.slice(start, end);
    var insert, selFrom, selTo;

    if (WRAP[action]) {
      var open = WRAP[action][0], close = WRAP[action][1];
      var inner = selected || '';
      insert = open + inner + close;
      selFrom = start + open.length;
      selTo = selFrom + inner.length;
    } else if (action === 'link') {
      var text = selected || 'text';
      insert = '[' + text + '](https://)';
      selFrom = start + text.length + 3;
      selTo = selFrom + 8;
    } else if (action === 'quote' || action === 'list') {
      var prefix = action === 'quote' ? '> ' : '- ';
      // Extend to whole lines so the prefix lands at the start of each line.
      var lineStart = value.lastIndexOf('\n', start - 1) + 1;
      var block = value.slice(lineStart, end);
      var prefixed = block.split('\n').map(function (l) { return prefix + l; }).join('\n');
      textarea.setRangeText(prefixed, lineStart, end, 'select');
      textarea.focus();
      textarea.dispatchEvent(new Event('input', { bubbles: true }));
      return;
    } else {
      return;
    }
    textarea.setRangeText(insert, start, end, 'end');
    textarea.focus();
    textarea.setSelectionRange(selFrom, selTo);
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
  }

  function showTab(editor, tab) {
    var textarea = editor.querySelector('textarea');
    var preview = editor.querySelector('[data-md-preview]');
    editor.querySelectorAll('[data-md-tab]').forEach(function (b) {
      b.setAttribute('aria-pressed', b.getAttribute('data-md-tab') === tab ? 'true' : 'false');
    });
    editor.querySelectorAll('[data-md]').forEach(function (b) { b.disabled = tab === 'preview'; });
    if (tab === 'write') {
      preview.classList.add('hidden');
      textarea.classList.remove('hidden');
      textarea.focus();
      return;
    }
    var token = '';
    try { token = JSON.parse(document.body.getAttribute('hx-headers') || '{}').RequestVerificationToken || ''; } catch (e) { /* no token */ }
    textarea.classList.add('hidden');
    preview.classList.remove('hidden');
    preview.textContent = '…';
    fetch('/social/preview', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
      body: new URLSearchParams({ text: textarea.value })
    }).then(function (r) { return r.ok ? r.text() : Promise.reject(r.status); })
      .then(function (html) { preview.innerHTML = html; prepareSpoilers(preview); })
      .catch(function () { preview.textContent = ''; showTab(editor, 'write'); });
  }

  function prepareSpoilers(root) {
    (root || document).querySelectorAll('x-spoiler:not([tabindex])').forEach(function (s) {
      s.setAttribute('tabindex', '0');
      s.setAttribute('role', 'button');
    });
  }

  document.addEventListener('click', function (e) {
    var t = e.target;
    if (!(t instanceof Element)) return;
    var tool = t.closest('[data-md]');
    if (tool) {
      var ed = tool.closest('[data-md-editor]');
      if (ed) apply(ed.querySelector('textarea'), tool.getAttribute('data-md'));
      return;
    }
    var tab = t.closest('[data-md-tab]');
    if (tab) {
      var ed2 = tab.closest('[data-md-editor]');
      if (ed2) showTab(ed2, tab.getAttribute('data-md-tab'));
      return;
    }
    var spoiler = t.closest('x-spoiler');
    if (spoiler && !t.closest('a')) spoiler.classList.toggle('revealed');
  });

  document.addEventListener('keydown', function (e) {
    var t = e.target;
    if (t instanceof Element && t.tagName === 'X-SPOILER' && (e.key === 'Enter' || e.key === ' ')) {
      e.preventDefault();
      t.classList.toggle('revealed');
    }
  });

  document.addEventListener('DOMContentLoaded', function () { prepareSpoilers(document); });
  document.addEventListener('htmx:afterSwap', function (e) { prepareSpoilers(e.target); });

  // Forms marked data-reset-on-success (the status composer) clear themselves after a successful post.
  document.addEventListener('htmx:afterRequest', function (e) {
    var form = e.detail.elt;
    if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-reset-on-success') || !e.detail.successful) return;
    form.reset();
    form.querySelectorAll('[data-md-editor]').forEach(function (ed) { showTab(ed, 'write'); });
  });

  // The "nothing here yet" placeholder disappears as soon as the first post is inserted.
  document.addEventListener('htmx:afterSwap', function (e) {
    if (e.target && e.target.id === 'feed-list') {
      var empty = document.getElementById('feed-empty');
      if (empty) empty.remove();
    }
  });
})();
