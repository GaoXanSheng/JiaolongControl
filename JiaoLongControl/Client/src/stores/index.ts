import {defineStore} from 'pinia'
import HOME from '@/assets/icon/HOME.png'
import CPU from '@/assets/icon/CPU.png'
import Fan from '@/assets/icon/Fan.png'
import Keyboard from '@/assets/icon/Keyboard.png'
import Settings from '@/assets/icon/Settings.png'
import AMD from '@/assets/icon/AMD.png'
import GPU from '@/assets/icon/GPU.png'
import FanCurve from '@/assets/icon/FanCurve.png'
import HOME_Page from '@/components/RightComponent/HOME.vue'
import CPU_Page from '@/components/RightComponent/CPU.vue'
import Fan_Page from '@/components/RightComponent/Fan.vue'
import Keyboard_Page from '@/components/RightComponent/KeyBoard.vue'
import Settings_Page from '@/components/RightComponent/Settings.vue'
import GPU_Page from "@/components/RightComponent/GPU.vue";
import RyzenSmu_Page from "@/components/RightComponent/RyzenSmu.vue";
import FanCurveEditor from "@/components/RightComponent/FanCurveEditor.vue";
const useStore = defineStore('store', {
    state: () => {
        return {
            SwitchPages: HomeCardType[0]!.eum,
            theme: 'light',
        }
    }
})
export const HomeCardType = [
    { title: '主页', icon: HOME, page: HOME_Page },
    { title: '中央处理器', icon: CPU, page: CPU_Page },
    { title: '图形处理器', icon: GPU, page: GPU_Page },
    { title: 'Ryzen SMU', icon: AMD, page: RyzenSmu_Page },
    { title: '风扇曲线', icon: FanCurve, page: FanCurveEditor },
    { title: '风扇', icon: Fan, page: Fan_Page },
    { title: '键盘', icon: Keyboard, page: Keyboard_Page },
    { title: '设置', icon: Settings, page: Settings_Page }
].map((item, index) => ({
    ...item,
    eum: index + 1
}));

export default useStore
