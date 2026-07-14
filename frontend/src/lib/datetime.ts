// Định dạng mốc thời gian UTC từ BE sang giờ Việt Nam (UTC+7).
// BE có thể trả chuỗi thiếu hậu tố 'Z' (UTC) → ép coi là UTC để không lệch theo timezone máy client.
export const formatVnTime = (utc: string | null | undefined): string => {
  if (!utc) return '';
  const iso = /[zZ]|[+-]\d{2}:\d{2}$/.test(utc) ? utc : `${utc}Z`;
  const d = new Date(iso);
  if (isNaN(d.getTime())) return utc;
  return d.toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour12: false });
};
