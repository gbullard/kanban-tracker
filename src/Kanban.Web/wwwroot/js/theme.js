(function () {
  var AVAILABLE = ['tokyonight', 'dracula', 'catppuccin', 'nord'];
  var link = document.getElementById('theme-link');
  var current = localStorage.getItem('kanban-theme') || 'tokyonight';

  if (AVAILABLE.indexOf(current) === -1) current = 'tokyonight';
  link.href = '/css/' + current + '.css';

  window.setTheme = function (name) {
    if (AVAILABLE.indexOf(name) === -1) return;
    localStorage.setItem('kanban-theme', name);
    link.href = '/css/' + name + '.css';
  };
})();