/** 面包屑 */
let breadcrumb = [
  {
    title: '首页',
    path: '/'
  }
]

// 解析面包屑数组
const resolveBreadcrumb = (page, parent) => {
  // 如果是子路由，则先继承父级路由的面包屑数组
  let bc = parent ? [...parent.meta.breadcrumb] : [...breadcrumb]

  if (page.meta.breadcrumb) {
    // 自定义
    bc.concat(page.breadcrumb)
  } else if (page.meta.title) {
    // 根据title设置
    bc.push({ title: page.meta.title, path: '' })
  }
  page.meta.breadcrumb = bc
}

// 解析路径
const resolvePath = (page, parent) => {
  if (!page.path.trim().startsWith('/') && parent) {
    let parentPath = parent.path.trim()
    let path = page.path.trim()
    if (parentPath.endsWith('/')) {
      parentPath = parentPath.substring(0, parentPath.length - 2)
    }
    page.path = `${parentPath}/${path}`
  }
}

// 递归解析嵌套路由
const resolveNestedRoute = (page, pages, parent) => {
  resolveBreadcrumb(page, parent)
  resolvePath(page, parent)

  page.children = []
  pages.map(p => {
    if (p.meta.parent === page.name) {
      page.children.push(resolveNestedRoute(p, pages, page))
    }
  })
  return page
}

/**
 * 单个页面配置信息转为路由信息
 * @param {Object} config 配置信息
 */
export const loadPage = config => {
  const { page, component } = config
  return {
    path: page.path,
    name: page.name,
    component: component,
    props: true,
    meta: {
      title: page.title,
      frameIn: page.frameIn,
      cache: page.cache,
      breadcrumb: page.breadcrumb,
      buttons: page.buttons,
      parent: page.parent
    }
  }
}

/**
 * @description 页面数组转为路由数组
 * @param {Object} module 模块信息
 * @param {Object} pages 页面数组
 */
export const pages2Routes = (module, pages) => {
  // 添加模块根面包屑
  breadcrumb.push({
    title: module.name,
    path: ''
  })

  const routes = pages
    .filter(p => !p.meta.parent)
    .map(page => resolveNestedRoute(page, pages))
  return routes
}

export default {
  loadPage,
  pages2Routes
}
