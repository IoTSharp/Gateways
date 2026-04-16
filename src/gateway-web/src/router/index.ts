import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
    { path: '/drivers', name: 'drivers', component: () => import('@/views/DriversView.vue') },
    { path: '/channels', name: 'channels', component: () => import('@/views/ChannelsView.vue') },
    { path: '/devices', name: 'devices', component: () => import('@/views/DevicesView.vue') },
    { path: '/points', name: 'points', component: () => import('@/views/PointsView.vue') },
    { path: '/tasks', name: 'tasks', component: () => import('@/views/TasksView.vue') },
    { path: '/transforms', name: 'transforms', component: () => import('@/views/TransformsView.vue') },
    { path: '/uploads', name: 'uploads', component: () => import('@/views/UploadsView.vue') },
    { path: '/runtime', name: 'runtime', component: () => import('@/views/RuntimeView.vue') },
  ],
})

export default router
