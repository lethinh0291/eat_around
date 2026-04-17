// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
	const menuToggle = document.getElementById('adminMenuToggle');
	if (!menuToggle) {
		return;
	}

	menuToggle.addEventListener('click', function () {
		const root = document.body;
		const isOpen = root.classList.toggle('admin-menu-open');
		menuToggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
	});

	document.addEventListener('click', function (event) {
		if (window.innerWidth > 1199 || !document.body.classList.contains('admin-menu-open')) {
			return;
		}

		const sidebar = document.getElementById('adminSidebar');
		if (!sidebar) {
			return;
		}

		const target = event.target;
		if (!(target instanceof Element)) {
			return;
		}

		if (!sidebar.contains(target) && !menuToggle.contains(target)) {
			document.body.classList.remove('admin-menu-open');
			menuToggle.setAttribute('aria-expanded', 'false');
		}
	});
})();
