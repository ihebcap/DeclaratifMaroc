import axios from 'axios';

export const API_BASE = (window as any).GOCOM_CONFIG?.API_BASE || import.meta.env.VITE_API_BASE || '/api';

const api = axios.create({
    baseURL: API_BASE,
});

api.interceptors.request.use((config) => {
    const token = sessionStorage.getItem('tva_user');
    if (token) {
        try {
            const user = JSON.parse(token);
            if (user.token) {
                config.headers.Authorization = `Bearer ${user.token}`;
            }
        } catch (e) {}
    }
    return config;
});

export default api;
