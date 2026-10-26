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
