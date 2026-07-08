window.foundryForgeDialog = {
  focusAndTrap: (dialog) => {
    if (!dialog) {
      return;
    }

    const selector = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const focusable = Array.from(dialog.querySelectorAll(selector));
    const first = focusable[0] || dialog;
    const last = focusable[focusable.length - 1] || dialog;
    const defaultFocus = dialog.querySelector('[data-autofocus]:not([disabled])') || first;

    defaultFocus.focus();

    dialog.onkeydown = (event) => {
      if (event.key !== 'Tab') {
        return;
      }

      if (focusable.length === 0) {
        event.preventDefault();
        dialog.focus();
        return;
      }

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
  }
};
