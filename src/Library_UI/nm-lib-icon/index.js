import './src/font/iconfont'
import iconData from './src/icon-data'
import Icon from './src/Icon'

Icon.install = function (Vue) {
  Vue.component('nm-icon', Icon)
}

export default Icon
export {iconData}