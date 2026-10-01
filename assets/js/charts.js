/**
 * Pure SVG Charts Generator
 * Fast, responsive, zero-dependency charts compliant with the Design System
 */

const Charts = {
  // 1. Line Chart (Doanh thu & Lưu lượng)
  renderLineChart(containerId, options = {}) {
    const el = document.getElementById(containerId);
    if (!el) return;

    const points = options.data || [12, 19, 15, 25, 22, 30, 38, 35, 45, 42, 55, 60];
    const labels = options.labels || ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'];
    const strokeColor = options.color || '#434CE8';
    const fillColor = options.fillColor || 'rgba(67, 76, 232, 0.08)';
    const height = options.height || 240;
    const width = 600;
    const padding = { top: 20, right: 20, bottom: 35, left: 35 };

    const maxVal = Math.max(...points) * 1.15 || 100;
    const minVal = 0;

    const chartW = width - padding.left - padding.right;
    const chartH = height - padding.top - padding.bottom;

    const stepX = chartW / (points.length - 1);
    const coords = points.map((val, idx) => {
      const x = padding.left + idx * stepX;
      const y = padding.top + chartH - ((val - minVal) / (maxVal - minVal)) * chartH;
      return { x, y, val, label: labels[idx] };
    });

    const dPath = coords.reduce((acc, pt, idx) => {
      return idx === 0 ? `M ${pt.x},${pt.y}` : `${acc} L ${pt.x},${pt.y}`;
    }, '');

    const areaPath = `${dPath} L ${coords[coords.length - 1].x},${padding.top + chartH} L ${coords[0].x},${padding.top + chartH} Z`;

    let gridLines = '';
    for (let i = 0; i <= 4; i++) {
      const y = padding.top + (chartH / 4) * i;
      gridLines += `<line x1="${padding.left}" y1="${y}" x2="${width - padding.right}" y2="${y}" stroke="#E2E8F0" stroke-dasharray="4 4" stroke-width="1" />`;
    }

    let circles = '';
    coords.forEach(pt => {
      circles += `
        <circle cx="${pt.x}" cy="${pt.y}" r="4" fill="#FFFFFF" stroke="${strokeColor}" stroke-width="2.5" class="chart-dot" data-val="${pt.val}">
          <title>${pt.label}: ${pt.val}</title>
        </circle>
      `;
    });

    let xLabels = '';
    coords.forEach((pt, idx) => {
      if (idx % 2 === 0 || coords.length <= 7) {
        xLabels += `<text x="${pt.x}" y="${height - 10}" fill="#64748B" font-size="11" text-anchor="middle" font-weight="500">${pt.label}</text>`;
      }
    });

    el.innerHTML = `
      <svg viewBox="0 0 ${width} ${height}" style="width: 100%; height: auto; overflow: visible;">
        ${gridLines}
        <path d="${areaPath}" fill="${fillColor}" />
        <path d="${dPath}" fill="none" stroke="${strokeColor}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
        ${circles}
        ${xLabels}
      </svg>
    `;
  },

  // 1b. Dual-axis Line Chart: 2 chỉ số khác thang đo (vd. Doanh thu triệu VNĐ & Số đơn hàng)
  //     series[0] dùng trục Y trái, series[1] dùng trục Y phải.
  //     options.series = [{ name, data, color, fillColor, unit }, { ... }]
  renderDualLineChart(containerId, options = {}) {
    const el = document.getElementById(containerId);
    if (!el) return;

    const labels = options.labels || [];
    const series = (options.series || []).slice(0, 2);
    if (!labels.length || !series.length) return;

    const height = options.height || 260;
    const width = 600;
    const padding = { top: 20, right: 46, bottom: 35, left: 46 };
    const chartW = width - padding.left - padding.right;
    const chartH = height - padding.top - padding.bottom;
    const stepX = labels.length > 1 ? chartW / (labels.length - 1) : 0;

    // Làm tròn mức trần trục Y cho dễ đọc; dữ liệu toàn 0 thì vẫn có thang đo
    const niceMax = (values) => {
      const max = Math.max(0, ...values.map(v => Number(v) || 0));
      if (max === 0) return 4;
      const raw = max * 1.15;
      const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
      const steps = [1, 2, 2.5, 5, 10];
      for (const s of steps) {
        if (raw <= s * magnitude) return s * magnitude;
      }
      return 10 * magnitude;
    };
    const formatTick = (v) => (Math.abs(v) >= 100 || Number.isInteger(v)) ? Math.round(v).toLocaleString('vi-VN') : v.toLocaleString('vi-VN', { maximumFractionDigits: 1 });

    const axes = series.map(s => ({ max: niceMax(s.data || []) }));

    let gridLines = '';
    let yLabels = '';
    for (let i = 0; i <= 4; i++) {
      const y = padding.top + (chartH / 4) * i;
      gridLines += `<line x1="${padding.left}" y1="${y}" x2="${width - padding.right}" y2="${y}" stroke="#E2E8F0" stroke-dasharray="4 4" stroke-width="1" />`;
      const ratio = (4 - i) / 4;
      yLabels += `<text x="${padding.left - 8}" y="${y + 4}" fill="${series[0].color}" font-size="10" text-anchor="end" font-weight="600">${formatTick(axes[0].max * ratio)}</text>`;
      if (series[1]) {
        yLabels += `<text x="${width - padding.right + 8}" y="${y + 4}" fill="${series[1].color}" font-size="10" text-anchor="start" font-weight="600">${formatTick(axes[1].max * ratio)}</text>`;
      }
    }

    let paths = '';
    let circles = '';
    series.forEach((s, sIdx) => {
      const data = s.data || [];
      const max = axes[sIdx].max;
      const coords = labels.map((label, idx) => {
        const val = Number(data[idx]) || 0;
        return {
          x: padding.left + idx * stepX,
          y: padding.top + chartH - (val / max) * chartH,
          val: val,
          label: label
        };
      });
      const dPath = coords.reduce((acc, pt, idx) => idx === 0 ? `M ${pt.x},${pt.y}` : `${acc} L ${pt.x},${pt.y}`, '');
      if (s.fillColor) {
        const areaPath = `${dPath} L ${coords[coords.length - 1].x},${padding.top + chartH} L ${coords[0].x},${padding.top + chartH} Z`;
        paths += `<path d="${areaPath}" fill="${s.fillColor}" />`;
      }
      paths += `<path d="${dPath}" fill="none" stroke="${s.color}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" ${sIdx === 1 ? 'stroke-dasharray="6 4"' : ''} />`;
      coords.forEach(pt => {
        circles += `<circle cx="${pt.x}" cy="${pt.y}" r="4" fill="#FFFFFF" stroke="${s.color}" stroke-width="2.5"><title>${pt.label} – ${s.name}: ${pt.val.toLocaleString('vi-VN')}${s.unit ? ' ' + s.unit : ''}</title></circle>`;
      });
    });

    let xLabels = '';
    labels.forEach((label, idx) => {
      if (idx % 2 === 0 || labels.length <= 7) {
        xLabels += `<text x="${padding.left + idx * stepX}" y="${height - 10}" fill="#64748B" font-size="11" text-anchor="middle" font-weight="500">${label}</text>`;
      }
    });

    el.innerHTML = `
      <svg viewBox="0 0 ${width} ${height}" style="width: 100%; height: auto; overflow: visible;">
        ${gridLines}
        ${yLabels}
        ${paths}
        ${circles}
        ${xLabels}
      </svg>
    `;
  },

  // 2. Bar Chart (Phân bổ đánh giá sao / Lượt đăng ký)
  renderBarChart(containerId, options = {}) {
    const el = document.getElementById(containerId);
    if (!el) return;

    const data = options.data || [
      { label: '5 sao', value: 85, color: '#10B981' },
      { label: '4 sao', value: 12, color: '#434CE8' },
      { label: '3 sao', value: 2, color: '#F59E0B' },
      { label: '2 sao', value: 1, color: '#F97316' },
      { label: '1 sao', value: 0, color: '#EF4444' }
    ];

    let barsHtml = '';
    data.forEach(item => {
      barsHtml += `
        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 12px;">
          <span style="width: 36px; font-size: 13px; font-weight: 600; color: #0F172A;">${item.label}</span>
          <div style="flex: 1; height: 10px; background-color: #F1F5F9; border-radius: 9999px; overflow: hidden;">
            <div style="width: ${item.value}%; height: 100%; background-color: ${item.color}; border-radius: 9999px; transition: width 0.6s ease;"></div>
          </div>
          <span style="width: 44px; text-align: right; font-size: 13px; font-weight: 700; color: #64748B;">${item.value}%</span>
        </div>
      `;
    });

    el.innerHTML = barsHtml;
  },

  // 3. Donut Chart (Tỷ trọng danh mục)
  renderDonutChart(containerId, options = {}) {
    const el = document.getElementById(containerId);
    if (!el) return;

    const segments = options.segments || [
      { label: 'Lập trình & CNTT', value: 45, color: '#434CE8' },
      { label: 'UI/UX Design', value: 25, color: '#3B82F6' },
      { label: 'AI & Data', value: 18, color: '#10B981' },
      { label: 'Marketing', value: 12, color: '#F59E0B' }
    ];

    const size = 180;
    const strokeWidth = 24;
    const radius = (size - strokeWidth) / 2;
    const circumference = 2 * Math.PI * radius;

    let accumulatedOffset = 0;
    let circlesHtml = '';

    segments.forEach(seg => {
      const strokeDasharray = `${(seg.value / 100) * circumference} ${circumference}`;
      const strokeDashoffset = -accumulatedOffset;
      accumulatedOffset += (seg.value / 100) * circumference;

      circlesHtml += `
        <circle cx="${size/2}" cy="${size/2}" r="${radius}" fill="none"
          stroke="${seg.color}" stroke-width="${strokeWidth}"
          stroke-dasharray="${strokeDasharray}" stroke-dashoffset="${strokeDashoffset}"
          style="transition: stroke-dasharray 0.6s ease;" />
      `;
    });

    let legendHtml = '';
    segments.forEach(seg => {
      legendHtml += `
        <div style="display: flex; align-items: center; justify-content: space-between; font-size: 13px; margin-bottom: 8px;">
          <div style="display: flex; align-items: center; gap: 8px;">
            <span style="width: 10px; height: 10px; border-radius: 3px; background-color: ${seg.color};"></span>
            <span style="color: #334155;">${seg.label}</span>
          </div>
          <strong style="color: #0F172A;">${seg.value}%</strong>
        </div>
      `;
    });

    el.innerHTML = `
      <div style="display: flex; align-items: center; gap: 24px; flex-wrap: wrap;">
        <div style="position: relative; width: ${size}px; height: ${size}px;">
          <svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" style="transform: rotate(-90deg);">
            ${circlesHtml}
          </svg>
          <div style="position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center;">
            <span style="font-size: 22px; font-weight: 800; color: #0F172A;">100%</span>
            <span style="font-size: 11px; color: #64748B; font-weight: 500;">Phân bổ</span>
          </div>
        </div>
        <div style="flex: 1; min-width: 160px;">
          ${legendHtml}
        </div>
      </div>
    `;
  }
};

if (typeof window !== 'undefined') {
  window.Charts = Charts;
}
