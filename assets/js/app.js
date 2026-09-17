/**
 * Core Application Interactions & Utilities
 * E-Learning Design System Engine
 */

document.addEventListener('DOMContentLoaded', () => {
  initModals();
  initTabs();
  initDropdowns();
  initToastContainer();
});

// 1. Modal Handler
function initModals() {
  document.querySelectorAll('[data-modal-target]').forEach(trigger => {
    trigger.addEventListener('click', (e) => {
      e.preventDefault();
      const modalId = trigger.getAttribute('data-modal-target');
      openModal(modalId);
    });
  });

  document.querySelectorAll('[data-modal-close]').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      const modal = btn.closest('.modal-overlay');
      if (modal) {
        closeModal(modal.id);
      }
    });
  });

  // Close on outside click
  document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', (e) => {
      if (e.target === overlay) {
        closeModal(overlay.id);
      }
    });
  });
}

function openModal(id) {
  const modal = document.getElementById(id);
  if (modal) {
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';
  }
}

function closeModal(id) {
  const modal = document.getElementById(id);
  if (modal) {
    modal.classList.remove('active');
    document.body.style.overflow = '';
  }
}

// 2. Tab Switcher
function initTabs() {
  document.querySelectorAll('.tab-nav').forEach(nav => {
    const btns = nav.querySelectorAll('.tab-btn');
    btns.forEach(btn => {
      btn.addEventListener('click', () => {
        const targetId = btn.getAttribute('data-tab');
        if (!targetId) return;

        // Toggle active button
        btns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');

        // Toggle target content
        const container = nav.parentElement;
        container.querySelectorAll('.tab-content').forEach(content => {
          content.style.display = content.id === targetId ? 'block' : 'none';
        });
      });
    });
  });
}

// 3. Dropdowns
function initDropdowns() {
  document.addEventListener('click', (e) => {
    const isTrigger = e.target.closest('[data-dropdown-toggle]');
    if (isTrigger) {
      const targetId = isTrigger.getAttribute('data-dropdown-toggle');
      const menu = document.getElementById(targetId);
      if (menu) {
        menu.classList.toggle('active');
      }
    } else {
      document.querySelectorAll('.dropdown-menu.active').forEach(m => m.classList.remove('active'));
    }
  });
}

// 4. Toast Notifications
function initToastContainer() {
  if (!document.getElementById('toast-container')) {
    const container = document.createElement('div');
    container.id = 'toast-container';
    container.style.cssText = `
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      pointer-events: none;
    `;
    document.body.appendChild(container);
  }
}

function showToast(message, type = 'success') {
  const container = document.getElementById('toast-container') || document.body;
  const toast = document.createElement('div');
  
  const colors = {
    success: { bg: '#ECFDF5', border: '#10B981', text: '#065F46', icon: '✓' },
    error: { bg: '#FEF2F2', border: '#EF4444', text: '#991B1B', icon: '✕' },
    warning: { bg: '#FFFBEB', border: '#F59E0B', text: '#92400E', icon: '⚠' },
    info: { bg: '#EEF2FF', border: '#434CE8', text: '#312E81', icon: 'ℹ' }
  };

  const c = colors[type] || colors.info;

  toast.style.cssText = `
    background: ${c.bg};
    border: 1px solid ${c.border};
    color: ${c.text};
    padding: 12px 20px;
    border-radius: 12px;
    font-size: 14px;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 10px;
    box-shadow: 0 10px 25px -5px rgba(0,0,0,0.1);
    transform: translateY(20px);
    opacity: 0;
    transition: all 0.25s ease;
    pointer-events: auto;
  `;

  toast.innerHTML = `<span style="font-weight:800">${c.icon}</span> <span>${message}</span>`;
  container.appendChild(toast);

  requestAnimationFrame(() => {
    toast.style.transform = 'translateY(0)';
    toast.style.opacity = '1';
  });

  setTimeout(() => {
    toast.style.transform = 'translateY(20px)';
    toast.style.opacity = '0';
    setTimeout(() => toast.remove(), 250);
  }, 3500);
}

// 5. Star Rating Component Helper
function initStarRating(containerId, onSelect) {
  const container = document.getElementById(containerId);
  if (!container) return;

  let currentRating = 0;
  container.innerHTML = '';
  container.style.cssText = 'display: flex; gap: 8px; cursor: pointer;';

  for (let i = 1; i <= 5; i++) {
    const star = document.createElement('span');
    star.innerHTML = '★';
    star.dataset.star = i;
    star.style.cssText = 'font-size: 28px; color: #CBD5E1; transition: color 0.15s ease;';

    star.addEventListener('mouseenter', () => highlight(i));
    star.addEventListener('mouseleave', () => highlight(currentRating));
    star.addEventListener('click', () => {
      currentRating = i;
      highlight(currentRating);
      if (onSelect) onSelect(currentRating);
    });

    container.appendChild(star);
  }

  function highlight(count) {
    const stars = container.querySelectorAll('span');
    stars.forEach((s, idx) => {
      s.style.color = idx < count ? '#F59E0B' : '#CBD5E1';
    });
  }

  return {
    setRating: (val) => {
      currentRating = val;
      highlight(val);
    },
    getRating: () => currentRating
  };
}

// 6. Modern Interactive Cart Warning Toast (Real-World E-Commerce UI/UX)
function showCartWarningToast(message, cartUrl) {
  initToastContainer();
  const container = document.getElementById('toast-container');
  if (!container) return;

  const existingWarning = container.querySelector('.cart-warning-toast-item');
  if (existingWarning) {
    existingWarning.remove();
  }

  const toast = document.createElement('div');
  toast.className = 'cart-warning-toast-item';
  toast.style.cssText = `
    background: #FFFBEB;
    border: 1px solid #FCD34D;
    border-left: 5px solid #F59E0B;
    color: #92400E;
    padding: 14px 18px;
    border-radius: 14px;
    font-size: 14px;
    display: flex;
    align-items: center;
    gap: 12px;
    box-shadow: 0 15px 30px -5px rgba(245, 158, 11, 0.2), 0 4px 12px rgba(0,0,0,0.08);
    transform: translateY(20px);
    opacity: 0;
    transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
    pointer-events: auto;
    max-width: 460px;
  `;

  const targetCartUrl = cartUrl || '/Cart';

  toast.innerHTML = `
    <div style="width: 36px; height: 36px; background: #FEF3C7; border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0; color: #D97706; font-size: 17px; font-weight: 800;">
      ⚠️
    </div>
    <div style="flex: 1; min-width: 0;">
      <div style="font-weight: 700; color: #78350F; font-size: 14px; margin-bottom: 2px;">Khóa học đã có trong giỏ hàng!</div>
      <div style="font-size: 12.5px; color: #B45309; line-height: 1.35; text-overflow: ellipsis; overflow: hidden; white-space: nowrap;">${message || 'Khóa học này đã được thêm vào giỏ hàng từ trước.'}</div>
    </div>
    <a href="${targetCartUrl}" style="background: #F59E0B; color: #FFFFFF; font-weight: 700; font-size: 12px; padding: 7px 13px; border-radius: 8px; text-decoration: none; white-space: nowrap; flex-shrink: 0; transition: background 0.2s;" onmouseover="this.style.background='#D97706'" onmouseout="this.style.background='#F59E0B'">
      Xem giỏ hàng →
    </a>
  `;

  container.appendChild(toast);

  requestAnimationFrame(() => {
    toast.style.transform = 'translateY(0)';
    toast.style.opacity = '1';
  });

  setTimeout(() => {
    if (toast.parentNode) {
      toast.style.transform = 'translateY(20px)';
      toast.style.opacity = '0';
      setTimeout(() => toast.remove(), 250);
    }
  }, 4500);
}

// Export globals
window.openModal = openModal;
window.closeModal = closeModal;
window.showToast = showToast;
window.showCartWarningToast = showCartWarningToast;
window.initStarRating = initStarRating;
