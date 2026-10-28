/**
 * admin-charts.js — Biểu đồ SVG có rê chuột xem số liệu cho khu Admin (Dashboard, Báo cáo hệ thống).
 *
 * AdminCharts.dualLine(el, { labels, series: [{ name, data, color, dashed, fill, format }], axes: [fmtTrái, fmtPhải], title(i) })
 *   Đường 2 trục: series[0] trục trái, series[1] trục phải. Rê chuột: đường dóng + khung số liệu của điểm gần nhất.
 * AdminCharts.donut(el, { segments: [{ label, value (%), detail, color }], centerValue, centerLabel, emptyText })
 *   Rê chuột vào phần màu hoặc dòng chú thích: giữa vòng hiện % và chi tiết.
 * AdminCharts.tips(root)
 *   Phần tử có data-tip (vd. thanh tỷ lệ) hiện khung số liệu khi rê chuột.
 */
(function () {
  'use strict';

  var fmtNum = function (v) { return Number(v || 0).toLocaleString('vi-VN'); };
  var fmtMoney = function (v) { return Number(v || 0).toLocaleString('vi-VN') + ' đ'; };
  var fmtShort = function (v) {
    if (v >= 1e9) return (v / 1e9).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' tỷ';
    if (v >= 1e6) return (v / 1e6).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' tr';
    if (v >= 1e3) return (v / 1e3).toLocaleString('vi-VN', { maximumFractionDigits: 0 }) + 'k';
    return fmtNum(v);
  };
  var esc = function (s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
  var niceMax = function (values) {
    var max = Math.max.apply(null, [0].concat(values.map(Number)));
    if (max <= 0) return 4;
    var raw = max * 1.15, mag = Math.pow(10, Math.floor(Math.log10(raw)));
    var steps = [1, 2, 2.5, 5, 10];
    for (var i = 0; i < steps.length; i++) if (raw <= steps[i] * mag) return steps[i] * mag;
    return 10 * mag;
  };

  function injectStyle() {
    if (document.getElementById('admin-charts-style')) return;
    var st = document.createElement('style');
    st.id = 'admin-charts-style';
    st.textContent =
      '.ac-chart{position:relative}.ac-chart svg{display:block;width:100%;height:auto}' +
      '.ac-tip{position:absolute;pointer-events:none;z-index:50;min-width:150px;padding:8px 10px;border-radius:10px;background:#0F172A;color:#fff;' +
      'font-size:12.5px;line-height:1.5;box-shadow:0 8px 20px rgba(15,23,42,.25);white-space:nowrap}' +
      '.ac-tip strong{display:block;margin-bottom:2px}.ac-tip i{display:inline-block;width:8px;height:8px;border-radius:50%;margin-right:6px}' +
      '.ac-float{position:fixed}' +
      '.ac-empty{text-align:center;color:var(--text-secondary,#64748B);padding:24px;font-size:13.5px}' +
      '.ac-donut{display:flex;align-items:center;gap:20px;flex-wrap:wrap}' +
      '.ac-ring{position:relative;width:180px;height:180px;flex:none}' +
      '.ac-ring circle.seg{cursor:pointer;transition:stroke-width .15s}.ac-ring circle.seg.hover{stroke-width:30}' +
      '.ac-center{position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;pointer-events:none;text-align:center;padding:0 30px}' +
      '.ac-center strong{font-size:21px;font-weight:800;color:#0F172A}.ac-center span{font-size:11.5px;color:#64748B;font-weight:500}' +
      '.ac-legend{flex:1;min-width:160px}' +
      '.ac-legend div{display:flex;justify-content:space-between;align-items:center;gap:8px;font-size:13px;padding:4px 6px;border-radius:6px}' +
      '.ac-legend div.hover{background:#F1F5F9}.ac-legend em{width:10px;height:10px;border-radius:3px;flex:none}' +
      '[data-tip]{cursor:default}';
    document.head.appendChild(st);
  }

  function dualLine(box, o) {
    injectStyle();
    box.classList.add('ac-chart');
    var labels = o.labels || [], series = (o.series || []).slice(0, 2);
    if (!labels.length || !series.length) { box.innerHTML = '<div class="ac-empty">Chưa có dữ liệu.</div>'; return; }
    var axes = o.axes || [fmtNum, fmtShort];
    var W = 640, H = o.height || 270, P = { top: 16, right: series.length > 1 ? 58 : 20, bottom: 34, left: 48 };
    var cw = W - P.left - P.right, ch = H - P.top - P.bottom;
    var maxes = series.map(function (s) { return niceMax(s.data); });
    var step = labels.length > 1 ? cw / (labels.length - 1) : 0;
    var x = function (i) { return P.left + (labels.length > 1 ? i * step : cw / 2); };
    var y = function (si, v) { return P.top + ch - Number(v || 0) / maxes[si] * ch; };

    var svg = '';
    for (var g = 0; g <= 4; g++) {
      var gy = P.top + ch / 4 * g, r = (4 - g) / 4;
      svg += '<line x1="' + P.left + '" y1="' + gy + '" x2="' + (W - P.right) + '" y2="' + gy + '" stroke="#E2E8F0" stroke-dasharray="4 4" />';
      svg += '<text x="' + (P.left - 8) + '" y="' + (gy + 4) + '" fill="' + series[0].color + '" font-size="10.5" font-weight="600" text-anchor="end">' + axes[0](maxes[0] * r) + '</text>';
      if (series[1]) svg += '<text x="' + (W - P.right + 8) + '" y="' + (gy + 4) + '" fill="' + series[1].color + '" font-size="10.5" font-weight="600">' + axes[1](maxes[1] * r) + '</text>';
    }
    series.forEach(function (s, si) {
      var d = s.data.map(function (v, i) { return (i ? 'L' : 'M') + x(i) + ',' + y(si, v); }).join(' ');
      if (s.fill) svg += '<path d="' + d + ' L' + x(s.data.length - 1) + ',' + (P.top + ch) + ' L' + x(0) + ',' + (P.top + ch) + ' Z" fill="' + s.fill + '" />';
      svg += '<path d="' + d + '" fill="none" stroke="' + s.color + '" stroke-width="3"' + (s.dashed ? ' stroke-dasharray="7 5"' : '') + ' stroke-linecap="round" stroke-linejoin="round" />';
    });
    svg += '<line class="ac-guide" x1="0" x2="0" y1="' + P.top + '" y2="' + (P.top + ch) + '" stroke="#94A3B8" stroke-width="1" visibility="hidden" />';
    labels.forEach(function (lb, i) {
      series.forEach(function (s, si) {
        svg += '<circle class="ac-dot" data-i="' + i + '" cx="' + x(i) + '" cy="' + y(si, s.data[i]) + '" r="4" fill="#fff" stroke="' + s.color + '" stroke-width="2.5" />';
      });
      if (labels.length <= 12 || i % 2 === 0) svg += '<text x="' + x(i) + '" y="' + (H - 10) + '" fill="#64748B" font-size="11" font-weight="500" text-anchor="middle">' + esc(lb) + '</text>';
    });
    box.innerHTML = '<svg viewBox="0 0 ' + W + ' ' + H + '" role="img" aria-label="' + esc(o.ariaLabel || 'Biểu đồ') + '">' + svg + '</svg><div class="ac-tip" hidden></div>';

    var svgEl = box.querySelector('svg'), tip = box.querySelector('.ac-tip'), guide = box.querySelector('.ac-guide');
    var show = function (i) {
      guide.setAttribute('x1', x(i)); guide.setAttribute('x2', x(i)); guide.setAttribute('visibility', 'visible');
      box.querySelectorAll('.ac-dot').forEach(function (c) { c.setAttribute('r', +c.dataset.i === i ? 6 : 4); });
      tip.innerHTML = '<strong>' + esc(o.title ? o.title(i) : labels[i]) + '</strong>' + series.map(function (s) {
        return '<div><i style="background:' + s.color + '"></i>' + esc(s.name) + ': <b>' + esc((s.format || fmtNum)(s.data[i])) + '</b></div>';
      }).join('');
      tip.hidden = false;
      var scale = svgEl.getBoundingClientRect().width / W;
      var top = Math.min.apply(null, series.map(function (s, si) { return y(si, s.data[i]); })) * scale - 10;
      var left = x(i) * scale + 12;
      if (left + tip.offsetWidth > box.clientWidth) left = x(i) * scale - tip.offsetWidth - 12;
      tip.style.left = Math.max(0, left) + 'px';
      tip.style.top = Math.max(0, top) + 'px';
    };
    var hide = function () {
      tip.hidden = true; guide.setAttribute('visibility', 'hidden');
      box.querySelectorAll('.ac-dot').forEach(function (c) { c.setAttribute('r', 4); });
    };
    var nearest = function (evt) {
      if (labels.length < 2) return 0;
      var rect = svgEl.getBoundingClientRect(), px = (evt.clientX - rect.left) / rect.width * W;
      return Math.max(0, Math.min(labels.length - 1, Math.round((px - P.left) / step)));
    };
    svgEl.addEventListener('mousemove', function (e) { show(nearest(e)); });
    svgEl.addEventListener('mouseleave', hide);
    svgEl.addEventListener('touchstart', function (e) { if (e.touches[0]) show(nearest(e.touches[0])); }, { passive: true });
  }

  function donut(box, o) {
    injectStyle();
    var segs = o.segments || [];
    if (!segs.length) { box.innerHTML = '<div class="ac-empty">' + esc(o.emptyText || 'Chưa có dữ liệu.') + '</div>'; return; }
    var size = 180, sw = 24, rad = (size - sw) / 2, circ = 2 * Math.PI * rad, acc = 0;
    var rings = segs.map(function (s, i) {
      var len = s.value / 100 * circ;
      var html = '<circle class="seg" data-i="' + i + '" cx="' + size / 2 + '" cy="' + size / 2 + '" r="' + rad + '" fill="none" stroke="' + s.color +
        '" stroke-width="' + sw + '" stroke-dasharray="' + len + ' ' + circ + '" stroke-dashoffset="' + (-acc) + '" />';
      acc += len; return html;
    }).join('');
    var legend = segs.map(function (s, i) {
      return '<div data-i="' + i + '"><span style="display:flex;align-items:center;gap:8px;"><em style="background:' + s.color + '"></em>' + esc(s.label) +
        '</span><strong>' + s.value.toLocaleString('vi-VN') + '%</strong></div>';
    }).join('');
    box.innerHTML = '<div class="ac-donut"><div class="ac-ring"><svg width="' + size + '" height="' + size + '" viewBox="0 0 ' + size + ' ' + size +
      '" style="transform:rotate(-90deg)">' + rings + '</svg><div class="ac-center"><strong></strong><span></span></div></div><div class="ac-legend">' + legend + '</div></div>';
    var value = box.querySelector('.ac-center strong'), label = box.querySelector('.ac-center span');
    var focus = function (i) {
      box.querySelectorAll('[data-i]').forEach(function (el) { el.classList.toggle('hover', +el.dataset.i === i); });
      if (i < 0) { value.textContent = o.centerValue || '100%'; label.textContent = o.centerLabel || ''; return; }
      value.textContent = segs[i].value.toLocaleString('vi-VN') + '%';
      label.textContent = segs[i].label + (segs[i].detail ? ' · ' + segs[i].detail : '');
    };
    box.querySelectorAll('[data-i]').forEach(function (el) {
      el.addEventListener('mouseenter', function () { focus(+el.dataset.i); });
      el.addEventListener('mouseleave', function () { focus(-1); });
    });
    focus(-1);
  }

  var floatTip;
  function tips(root) {
    injectStyle();
    if (!floatTip) {
      floatTip = document.createElement('div');
      floatTip.className = 'ac-tip ac-float';
      floatTip.hidden = true;
      document.body.appendChild(floatTip);
    }
    (root || document).querySelectorAll('[data-tip]').forEach(function (el) {
      if (el.dataset.tipReady) return;
      el.dataset.tipReady = '1';
      el.addEventListener('mousemove', function (e) {
        floatTip.innerHTML = el.dataset.tip;
        floatTip.hidden = false;
        var left = e.clientX + 14;
        if (left + floatTip.offsetWidth > window.innerWidth - 8) left = e.clientX - floatTip.offsetWidth - 14;
        floatTip.style.left = left + 'px';
        floatTip.style.top = (e.clientY - floatTip.offsetHeight - 10) + 'px';
      });
      el.addEventListener('mouseleave', function () { floatTip.hidden = true; });
    });
  }

  window.AdminCharts = { dualLine: dualLine, donut: donut, tips: tips, fmtNum: fmtNum, fmtMoney: fmtMoney, fmtShort: fmtShort, esc: esc };
})();
