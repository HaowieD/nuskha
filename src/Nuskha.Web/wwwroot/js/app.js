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
